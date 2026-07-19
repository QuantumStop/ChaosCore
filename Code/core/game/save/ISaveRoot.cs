namespace Core;

/// <summary>
/// Marks a runtime root that is excluded from scene patching, 
/// but should still be saved as a whole serialized subtree.
/// </summary>
public interface ISaveRoot
{
	string SaveRootKey { get; }
	GameObject SaveRootObject { get; }

	void BeforeSaveRoot() { }
	void AfterLoadRoot() { }
}
