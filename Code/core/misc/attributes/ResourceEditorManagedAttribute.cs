namespace Core;

using System;

/// <summary>
/// Allows ResourceEditor to edit GameResource's class metadata and serialized properties.
/// Members outside the authored properties and their defaults are preserved.
/// </summary>
[AttributeUsage( AttributeTargets.Class, Inherited = false )]
public sealed class ResourceEditorManagedAttribute : Attribute { }