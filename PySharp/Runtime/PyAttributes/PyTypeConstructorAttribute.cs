using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
internal sealed class PyTypeConstructorAttribute : PyAttribute
{
    public PyTypeConstructorAttribute()
    {
        DoNotGenerateConstructor = false;
        AccessModifier = "private";
    }

    public bool DoNotGenerateConstructor { get; set; }
    public string AccessModifier { get; set; }
}

