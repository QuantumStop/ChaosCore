
namespace Core;

public class info_target : BaseEntity
{
	protected virtual string GetModel() { return "models/editor/info_target.vmdl"; }
	protected override string GetEditorVis() { return GetModel(); }
}
