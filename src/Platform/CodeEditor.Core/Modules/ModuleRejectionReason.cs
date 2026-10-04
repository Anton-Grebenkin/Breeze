namespace CodeEditor.Core.Modules;

public enum ModuleRejectionReason
{
    /// <summary>A module with this id already exists; the first one stays.</summary>
    DuplicateId,

    /// <summary>A dependency is not among the modules.</summary>
    MissingDependency,

    /// <summary>The module is on a dependency cycle.</summary>
    CircularDependency,

    /// <summary>The module depends on a rejected module.</summary>
    DependencyRejected,
}
