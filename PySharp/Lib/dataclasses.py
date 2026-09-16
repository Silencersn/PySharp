# dataclasses.py -- a dataclass decorator implementation for PySharp.
# Supports field annotations, field() (with metadata and kw_only), base
# field merging, InitVar/KW_ONLY markers, __post_init__, __match_args__,
# and automatic __init__/__repr__/__eq__.

class _Miss:
    def __repr__(self):
        return '<dataclasses._MISSING>'

_MISSING = _Miss()

# _process_class shadows the repr builtin with its repr= parameter
_repr = repr

# field types
_FIELD = 'field'
_FIELD_INITVAR = 'initvar'

_POST_INIT_NAME = '__post_init__'


class InitVar:
    def __init__(self, type=None):
        self.type = type

    def __class_getitem__(cls, item):
        return InitVar(item)

    def __repr__(self):
        if self.type is None:
            return 'dataclasses.InitVar'
        return 'dataclasses.InitVar[' + repr(self.type) + ']'


class KW_ONLY:
    def __class_getitem__(cls, item):
        return KW_ONLY()

    def __repr__(self):
        return 'dataclasses.KW_ONLY'


class _MappingProxy:
    # stand-in for types.MappingProxyType: a read-only-looking view that
    # CPython wraps field metadata in
    def __init__(self, data):
        self._data = data

    def __getitem__(self, key):
        return self._data[key]

    def get(self, key, default=None):
        return self._data.get(key, default)

    def keys(self):
        return self._data.keys()

    def values(self):
        return self._data.values()

    def items(self):
        return self._data.items()

    def __iter__(self):
        return iter(self._data)

    def __len__(self):
        return len(self._data)

    def __contains__(self, key):
        return key in self._data

    def __eq__(self, other):
        return self._data == other

    def __repr__(self):
        return 'mappingproxy(' + repr(self._data) + ')'


class Field:
    def __init__(self, default, default_factory, init, repr, hash, compare, metadata, kw_only):
        self.name = None
        self.type = _MISSING
        self.default = default
        self.default_factory = default_factory
        self.init = init
        self.repr = repr
        self.hash = hash
        self.compare = compare
        if isinstance(metadata, _MappingProxy):
            # _get_field re-wraps an existing Field spec; stay idempotent
            self.metadata = metadata
        else:
            self.metadata = _MappingProxy({} if metadata is None else metadata)
        self.kw_only = kw_only
        self._field_type = _FIELD

    def __set_name__(self, owner, name):
        self.name = name

    def __repr__(self):
        return 'Field(...)'


def field(*, default=_MISSING, default_factory=_MISSING, init=True, repr=True,
          hash=None, compare=True, metadata=None, kw_only=_MISSING, doc=None):
    if default is not _MISSING and default_factory is not _MISSING:
        raise ValueError('cannot specify both default and default_factory')
    return Field(default, default_factory, init, repr, hash, compare,
                 metadata, kw_only)


def _dc_repr(self, cls_name, names):
    parts = []
    for n in names:
        parts.append(n + '=' + repr(getattr(self, n)))
    return cls_name + '(' + ', '.join(parts) + ')'


def _dc_eq(self, other, names):
    if other is self:
        return True
    if type(other) is not type(self):
        return NotImplemented
    for n in names:
        if getattr(self, n) != getattr(other, n):
            return False
    return True


def _set_init(cls, all_fields):
    if '__init__' in cls.__dict__:
        return
    has_post_init = hasattr(cls, _POST_INIT_NAME)
    self_name = '__dataclass_self__' if 'self' in tuple(f.name for f in all_fields) else 'self'
    std = []
    kwo = []
    for fl in all_fields:
        if fl.init:
            if fl.kw_only:
                kwo.append(fl)
            else:
                std.append(fl)

    params = [self_name]
    defaults_list = []
    factories_list = []
    body_lines = []

    def emit(fl):
        # emit the __init__ parameter and the assignment line of one
        # field; InitVar fields take a parameter but store nothing, and
        # non-init factory fields are initialized by calling the factory
        name = fl.name
        is_real = fl._field_type is not _FIELD_INITVAR
        if fl.default_factory is not _MISSING:
            factories_list.append(fl.default_factory)
            idx = len(factories_list) - 1
            if fl.init:
                params.append(name + '=__dc_sentinel')
            if is_real:
                if fl.init:
                    body_lines.append('    ' + self_name + '.' + name + ' = ' + name + ' if ' + name + ' is not __dc_sentinel else __dc_factories[' + str(idx) + ']()')
                else:
                    body_lines.append('    ' + self_name + '.' + name + ' = __dc_factories[' + str(idx) + ']()')
        elif fl.default is not _MISSING:
            if fl.init:
                d_idx = len(defaults_list)
                defaults_list.append(fl.default)
                params.append(name + '=__dc_defaults[' + str(d_idx) + ']')
                if is_real:
                    body_lines.append('    ' + self_name + '.' + name + ' = ' + name)
        else:
            if fl.init:
                params.append(name)
                if is_real:
                    body_lines.append('    ' + self_name + '.' + name + ' = ' + name)

    for fl in std:
        emit(fl)
    if kwo:
        params.append('*')
        for fl in kwo:
            emit(fl)
    for fl in all_fields:
        if not fl.init and fl._field_type is not _FIELD_INITVAR and fl.default_factory is not _MISSING:
            emit(fl)

    if has_post_init:
        initvar_names = []
        for fl in all_fields:
            if fl._field_type is _FIELD_INITVAR:
                initvar_names.append(fl.name)
        body_lines.append('    ' + self_name + '.' + _POST_INIT_NAME + '(' + ', '.join(initvar_names) + ')')

    if not body_lines:
        body_lines.append('    pass')
    src = 'def __init__(' + ', '.join(params) + '):\n' + '\n'.join(body_lines) + '\n'
    g = dict(globals())
    g['__dc_defaults'] = defaults_list
    g['__dc_factories'] = factories_list
    g['__dc_sentinel'] = _MISSING
    exec(src, g)
    setattr(cls, '__init__', g['__init__'])


def _set_repr(cls, field_list):
    if '__repr__' in cls.__dict__:
        return
    names = []
    for fl in field_list:
        if fl.repr:
            names.append(fl.name)
    g = dict(globals())
    g['_dc_cls_name'] = cls.__name__
    g['_dc_names'] = tuple(names)
    src = 'def __repr__(self):\n    return _dc_repr(self, _dc_cls_name, _dc_names)\n'
    exec(src, g)
    setattr(cls, '__repr__', g['__repr__'])


def _set_eq(cls, field_list):
    if '__eq__' in cls.__dict__:
        return
    names = []
    for fl in field_list:
        if fl.compare:
            names.append(fl.name)
    g = dict(globals())
    g['_dc_names'] = tuple(names)
    src = 'def __eq__(self, other):\n    return _dc_eq(self, other, _dc_names)\n'
    exec(src, g)
    setattr(cls, '__eq__', g['__eq__'])


def _set_new_attribute(cls, name, value):
    # CPython _set_new_attribute: never overwrites an existing attribute
    # in the class dict
    if name in cls.__dict__:
        return
    setattr(cls, name, value)


_UNHASHABLE_BASES = (list, dict, set, bytearray)


def _reject_mutable_default(default, name):
    # CPython _get_field rejects mutable defaults during field
    # collection, using "the default's class __hash__ is None" as the
    # mutability proxy; this runtime exposes no None-hash marker, so
    # match the unhashable builtin bases across the MRO instead
    for base in type(default).__mro__:
        if base in _UNHASHABLE_BASES:
            raise ValueError('mutable default ' + str(type(default)) + ' for field ' + name + ' is not allowed: use default_factory')


def _is_initvar(a_type):
    if a_type is InitVar or type(a_type) is InitVar:
        return True
    if isinstance(a_type, str) and (a_type == 'InitVar' or a_type.startswith('InitVar[')):
        return True
    return False


def _is_kw_only(a_type):
    if a_type is KW_ONLY or type(a_type) is KW_ONLY:
        return True
    if isinstance(a_type, str) and (a_type == 'KW_ONLY' or a_type == 'dataclasses.KW_ONLY'):
        return True
    return False


def _get_field(cls, a_name, a_type, default_kw_only):
    default = _MISSING
    default_factory = _MISSING
    f_init = True
    f_repr = True
    f_compare = True
    f_hash = None
    f_metadata = None
    f_kw_only = _MISSING
    if a_name in cls.__dict__:
        value = cls.__dict__[a_name]
        if isinstance(value, Field):
            default = value.default
            default_factory = value.default_factory
            f_init = value.init
            f_repr = value.repr
            f_compare = value.compare
            f_hash = value.hash
            f_metadata = value.metadata
            f_kw_only = value.kw_only
        else:
            default = value
    if default is not _MISSING:
        _reject_mutable_default(default, a_name)
    if f_kw_only is _MISSING:
        f_kw_only = default_kw_only
    fl = Field(default, default_factory, f_init, f_repr, f_hash, f_compare, f_metadata, f_kw_only)
    fl.name = a_name
    fl.type = a_type
    if _is_initvar(a_type):
        fl._field_type = _FIELD_INITVAR
    return fl


def _process_class(cls, init, repr, eq, frozen, order, match_args, kw_only):
    if frozen:
        raise TypeError('frozen dataclasses are not supported yet')
    if order:
        raise TypeError('order dataclasses are not supported yet')

    # base fields first, in reverse MRO order (excluding the class
    # itself), so more derived definitions override earlier ones
    fields = {}
    mro = cls.__mro__
    idx = len(mro) - 1
    while idx > 0:
        b = mro[idx]
        base_fields = getattr(b, '__dataclass_fields__', None)
        if base_fields is not None:
            for f in base_fields.values():
                fields[f.name] = f
        idx -= 1

    ann = getattr(cls, '__annotations__', None)
    if ann is None:
        ann = {}
    default_kw_only = kw_only
    kw_seen = False
    for name in ann:
        a_type = ann[name]
        # a KW_ONLY annotation switches all following fields to
        # keyword-only and is not a field itself
        if _is_kw_only(a_type):
            if kw_seen:
                raise TypeError(_repr(name) + ' is KW_ONLY, but KW_ONLY has already been specified')
            kw_seen = True
            default_kw_only = True
            continue
        fl = _get_field(cls, name, a_type, default_kw_only)
        fields[name] = fl
        # a Field class attribute is the field's spec: replace it with
        # the real default so normal introspection sees the value
        if name in cls.__dict__ and isinstance(cls.__dict__[name], Field):
            if fl.default is _MISSING:
                delattr(cls, name)
            else:
                setattr(cls, name, fl.default)

    seen_default = None
    for fl in fields.values():
        # the default-order rule applies only to non-keyword-only fields
        if fl.init and not fl.kw_only and fl._field_type is not _FIELD_INITVAR:
            has_default = fl.default is not _MISSING or fl.default_factory is not _MISSING
            if not has_default:
                if seen_default is not None:
                    raise TypeError('non-default argument ' + _repr(fl.name) + ' follows default argument ' + _repr(seen_default.name))
            else:
                seen_default = fl

    if init:
        _set_init(cls, list(fields.values()))
    if repr:
        _set_repr(cls, [f for f in fields.values() if f._field_type is _FIELD])
    if eq:
        _set_eq(cls, [f for f in fields.values() if f._field_type is _FIELD])

    setattr(cls, '__dataclass_fields__', fields)

    if match_args:
        _set_new_attribute(cls, '__match_args__',
                           tuple(f.name for f in fields.values() if f.init and not f.kw_only))
    return cls


def dataclass(cls=None, init=True, repr=True, eq=True, frozen=False, order=False,
              match_args=True, kw_only=False):
    def wrap(cls):
        return _process_class(cls, init, repr, eq, frozen, order, match_args, kw_only)
    if cls is None:
        return wrap
    return wrap(cls)
