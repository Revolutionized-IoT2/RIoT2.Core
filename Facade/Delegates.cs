using System;

namespace RIoT2.Core
{
    [Obsolete("Orchestrator-only; the orchestrator has its own typed store since 1.11.0 (I26). Removed with Newtonsoft (I45).")]
    public delegate void StoredObjectEventHandler(Type type, dynamic obj, OperationType changeType);
}
