# dataclasses.py -- a minimal dataclass decorator implementation for PySharp.
# Supports basic field annotations, field(), and automatic __init__/__repr__/__eq__.

class _Miss:
    def __repr__(self):
        return '<dataclasses._MISSING>'

_MISSING = _Miss()


class Field:
    def __init__(self, default=_MISSING, default_factory=_MISSING, init=True, repr=True, compare=True, hash=None):
        self.name = None
        self.type = _MISSING
        self.default = default
        self.default_factory = default_factory
        self.init = init
        self.repr = repr
        self.compare = compare
        self.hash = hash

    def __set_name__(self, owner, name):
        self.name = name

    def __repr__(self):
        return 'Field(...)'


def field(default=_MISSING, default_factory=_MISSING, init=True, repr=True, compare=True, hash=None):
    if default is not _MISSING and default_factory is not _MISSING:
        raise TypeError('cannot specify both default and default_factory')
    return Field(default, default_factory, init, repr, compare, hash)


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


def _set_init(cls, field_list):
    if '__init__' in cls.__dict__:
        return
    init_fields = []
    for fl in field_list:
        if fl.init:
            init_fields.append(fl)
    params = ['self']
    defaults_list = []
    factories_list = []
    body_lines = []
    for fl in init_fields:
        name = fl.name
        if fl.default_factory is not _MISSING:
            params.append(name + '=__dc_sentinel')
            factories_list.append(fl.default_factory)
            idx = len(factories_list) - 1
            body_lines.append('    self.' + name + ' = ' + name + ' if ' + name + ' is not __dc_sentinel else __dc_factories[' + str(idx) + ']()')
        elif fl.default is not _MISSING:
            d_idx = len(defaults_list)
            defaults_list.append(fl.default)
            params.append(name + '=__dc_defaults[' + str(d_idx) + ']')
            body_lines.append('    self.' + name + ' = ' + name)
        else:
            params.append(name)
            body_lines.append('    self.' + name + ' = ' + name)
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


def _process_class(cls, init, repr, eq, frozen, order):
    if frozen:
        raise TypeError('frozen dataclasses are not supported yet')
    if order:
        raise TypeError('order dataclasses are not supported yet')
    ann = getattr(cls, '__annotations__', None)
    if ann is None:
        ann = {}
    field_list = []
    for name in ann:
        default = _MISSING
        default_factory = _MISSING
        f_init = True
        f_repr = True
        f_compare = True
        if name in cls.__dict__:
            value = cls.__dict__[name]
            if isinstance(value, Field):
                default = value.default
                default_factory = value.default_factory
                f_init = value.init
                f_repr = value.repr
                f_compare = value.compare
            else:
                default = value
        fl = Field(default, default_factory, f_init, f_repr, f_compare)
        fl.name = name
        fl.type = ann[name]
        field_list.append(fl)
    seen_default = False
    for fl in field_list:
        has_default = fl.default is not _MISSING or fl.default_factory is not _MISSING
        if not has_default and fl.init:
            if seen_default:
                raise TypeError('non-default argument follows default argument')
        elif has_default:
            seen_default = True
    for fl in field_list:
        name = fl.name
        if fl.default is not _MISSING:
            setattr(cls, name, fl.default)
        elif fl.default_factory is not _MISSING:
            if name in cls.__dict__:
                delattr(cls, name)
        else:
            if name in cls.__dict__:
                delattr(cls, name)
    if init:
        _set_init(cls, field_list)
    if repr:
        _set_repr(cls, field_list)
    if eq:
        _set_eq(cls, field_list)
    _dcf = {}
    for fl in field_list:
        _dcf[fl.name] = fl
    setattr(cls, '__dataclass_fields__', _dcf)
    return cls


def dataclass(cls=None, init=True, repr=True, eq=True, frozen=False, order=False):
    def wrap(cls):
        return _process_class(cls, init, repr, eq, frozen, order)
    if cls is None:
        return wrap
    return wrap(cls)
