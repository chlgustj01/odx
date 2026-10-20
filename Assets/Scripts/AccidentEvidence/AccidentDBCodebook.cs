using System;
using System.Collections.Generic;
using System.Linq;

[Serializable] public sealed class AccidentCodeOption { public string code,label;public bool unknown; }
[Serializable] public sealed class AccidentCodeField { public string column,displayName;public List<AccidentCodeOption> options=new List<AccidentCodeOption>(); }
[Serializable] public sealed class AccidentDBCodebook
{
    public List<AccidentCodeField> fields=new List<AccidentCodeField>();
    public AccidentCodeField Field(string column)=>fields.FirstOrDefault(f=>f.column==column);
    public string Normalize(string column,string code)
    {var o=Field(column)?.options.FirstOrDefault(x=>x.code==code);return o==null||o.unknown?null:o.code;}
    public string Label(string column,string code)
    {if(string.IsNullOrEmpty(code))return "UNKNOWN";var o=Field(column)?.options.FirstOrDefault(x=>x.code==code);return o==null||o.unknown?"UNKNOWN":o.label;}
}
