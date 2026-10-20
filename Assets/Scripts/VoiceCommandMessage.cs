using System;

[Serializable]
public class VoiceCommandMessage
{
    public string schema_version;
    public string message_type;
    public string command_id;
    public string voice_text;
    public string action;
    public string direction;
    public float? target_value;
    public string target_unit;
    public string parse_status;
}
