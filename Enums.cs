namespace RIoT2.Core
{
    public enum ValueType
    {
        Boolean = 0,
        Text = 1,
        Number = 2,
        Entity = 3,
        TextArray = 4
    }

    public enum RuleType
    {
        Trigger = 0,
        Function = 1,
        Condition = 2,
        Output = 3
    }

    public enum ConditionType
    {
        IfElse = 0,
        Switch = 1
    }

    public enum FlowOperator
    {
        Continue = 0,
        Stop = 1,
        Jump = 2
    }

    public enum OutputOperation
    {
        Toggle = 1,         //invert current values 
        Set_on = 2,         //write true to output
        Set_off = 3,        //write false to output
        Set_value = 4,      //simply write condition values 
        Pulse_down = 5,     //set state to false for set time
        Pulse_up = 6,        //set state to true for set time
        Variable = 7
    }
    public enum MqttTopic
    {
        NodeOnline = 1,
        Configuration = 2,
        Command = 3,
        Report = 4,
        OrchestratorOnline = 5
    }

    public enum FileOrFolder
    {
        File = 0,
        Folder = 1
    }

    public enum DocumentType
    {
        Undefined = 0,
        Music = 1,
        Video = 2,
        Photo = 3
    }

    public enum DashboardComponentType
    {
        Button = 0,
        Chart_line = 1,
        Chart_bar = 2,
        Chart_doughnut = 3,
        Chart_pie = 4,
        NumericValue = 5,
        Image = 6,
        Timeline = 7,
        SlideButton = 8,
        State = 9,
        Switch = 10
    }

    public enum DashboardComponentSize
    {
        Small = 3,
        Half = 6,
        Large = 9,
        Full = 12
    }

    public enum OperationType
    {
        Created = 1,
        Read = 2,
        Updated = 3,
        Deleted = 4
    }

    public enum ServiceEvent
    {
        Started = 1,
        Stopped = 2,
    }
}
