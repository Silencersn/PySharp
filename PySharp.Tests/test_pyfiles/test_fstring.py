a = f'''Level1{
    f"Level2{ 
        f'''Level3{
            f"{'Innermost'}End"
        }'''
    } Level2End"
} Level1End'''
assert a == "Level1Level2Level3InnermostEnd Level2End Level1End"

b = f'''DictExample{
{
        f'key1': f"value{ f'''nested{123}''' }",
        f"key2": f'''{ f"{456}" }'''
    }
}'''
assert b == "DictExample{'key1': 'valuenested123', 'key2': '456'}"

c = f'''List{
    [
        f"Element1{ f'''Inner{789}''' }",
        f'''Element2{ f"{'abc'}" }''',
        f"{ f'''Element3''' }"
    ]
} End'''
assert c == "List['Element1Inner789', 'Element2abc', 'Element3'] End"

d = f'''Start{
    f"Middle{
        f'''Deeper{
            f"Deepest{ 
                [f'ListItem{i}' for i in range(3)]
            }"
        }'''
    }"
}'''
assert d == "StartMiddleDeeperDeepest['ListItem0', 'ListItem1', 'ListItem2']"

e = f'''Set{
    {
        f"SetItem{1}",
        f'''SetItem{ f"{2}" }''',
        f"{ f'''SetItem{3}''' }"
    }
}'''
assert (e == "Set{'SetItem1', 'SetItem2', 'SetItem3'}"
        or e == "Set{'SetItem1', 'SetItem3', 'SetItem2'}"
        or e == "Set{'SetItem2', 'SetItem1', 'SetItem3'}"
        or e == "Set{'SetItem2', 'SetItem3', 'SetItem1'}"
        or e == "Set{'SetItem3', 'SetItem1', 'SetItem2'}"
        or e == "Set{'SetItem3', 'SetItem2', 'SetItem1'}")

def func(x):
    return f"FuncResult{x}"

f = f'''FuncCallExample{
    func(
        f"Arg{ 
            f'''NestedArg{'Value'}'''
        }"
    )
}'''
assert f == "FuncCallExampleFuncResultArgNestedArgValue"

g = f'''MixedExample{
    {
        'dict_key': f'''DictValue{
            [
                f"ListItem{ 
                    f'''TupleItem{(
                        'a', 'b', f'c'
                    )}'''
                }"
            ]
        }'''
    }
}'''
assert g == "MixedExample{'dict_key': 'DictValue[\"ListItemTupleItem(\\'a\\', \\'b\\', \\'c\\')\"]'}"

h = f'''Condition{
    f"TrueBranch{ 
        f'''NestedTrue{123}'''
    }" if True else f"FalseBranch{ 
        f'''NestedFalse{456}'''
    }"
}'''
assert h == "ConditionTrueBranchNestedTrue123"

i = '' '' '' f""
j = "1" f" " f" " f"12{1}34"
k = f"\"" f" {1}" '123'
assert i == ""
assert j == "1  12134"
assert k == "\" 1123"
