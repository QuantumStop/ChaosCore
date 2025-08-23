using Editor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

[CanEdit("sched")]
[EditorApp("Npc Schedule Editor", "calendar_month", "With which to edit ai tables")]
public class NpcScheduleEditor : BaseWindow, IAssetEditor {
	public bool CanOpenMultipleAssets => false;

	private Asset CurrentFile {get; set;}
	private bool Dirty {get; set;}

	private List<ScheduleInfo> Schedules = new List<ScheduleInfo>(); 
	private struct ScheduleInfo {
		public ScheduleInfo() { }
		public List<TaskInfo> Tasks = new List<TaskInfo>();
		public string Name = "Untitled Schedule";
		public string Description = "";
		public bool Collapsed = false;
	}
	private struct TaskInfo {
		public TaskInfo() { }
		public bool HasValidTaskHandler = false;
		public HandlerInfo TaskHandler;
		public List<HandlerInfo> PreviousTaskHandlers = new List<HandlerInfo>();
		public string Description = "";
		public bool Collapsed = false;
		public List<ConditionInfo> Conditions = new List<ConditionInfo>();
	}
	private struct ConditionInfo {
		public ConditionInfo() { }
		public bool HasValidConditionHandler = false;
		public HandlerInfo ConditionHandler;
		public List<HandlerInfo> PreviousConditionHandlers = new List<HandlerInfo>();
	}

	private List<HandlerInfo> TaskHandlers = new List<HandlerInfo>();
	private List<HandlerInfo> ConditionHandlers = new List<HandlerInfo>();
	public struct HandlerInfo {
		public string MethodName;
		public Dictionary<ParameterInfo, object> Parameters;
	}

	private Layout PageBody;
	public NpcScheduleEditor() {
		WindowTitle = "Schedule Editor";
		SetWindowIcon("emoji_people");

		Size = new Vector2(600f, 800f);
		Layout = Layout.Column();

		AddNewSchedule();
	}

	public void DrawUI(bool makeDirty = true) {
		Layout.Clear(true);

		if (makeDirty)
			Dirty = true;

		Layout.Margin = 16f;
		var fileactions = Layout.AddRow();
		var savebutton = new Button.Primary("Save");
		savebutton.Icon = "save";
		savebutton.Pressed += delegate {AssetSave(false);};
		fileactions.Add(savebutton);
		fileactions.AddStretchCell();

		Layout.AddSpacingCell(10f);
		var scroll = new ScrollArea(null);
		scroll.Canvas = new Widget(scroll);
		scroll.Canvas.Layout = Layout.Column();
		PageBody = scroll.Canvas.Layout;
		PageBody.Margin = 0f;
		PageBody.Alignment = TextFlag.LeftTop;

		UpdateTaskhandlerList();
		UpdateConditionHandlerList();

		for (int i = 0; i < Schedules.Count; i++) {
			var scheduleindex = i;
			var schedule = Schedules[scheduleindex];

			var scheduleheader = PageBody.AddRow();
			scheduleheader.Alignment = TextFlag.Left;
			scheduleheader.Spacing = 5f;

			var schedcollapsebutton = new IconButton("keyboard_arrow_down");
			schedcollapsebutton.OnClick += delegate {ScheduleToggleCollapsed(scheduleindex);};
			scheduleheader.Add(schedcollapsebutton);
			var schedulename = new Label(scheduleindex + ":");
			scheduleheader.Add(schedulename);

			if (!schedule.Collapsed) {
				var schededitname = new LineEdit();
				schededitname.Text = schedule.Name;
				schededitname.TextChanged += delegate(string newstring) {ScheduleNameChanged(scheduleindex, newstring);};
				scheduleheader.Add(schededitname);
			} else {
				scheduleheader.AddStretchCell();
			}

			var schedulereorderup = new IconButton("arrow_upward");
			if (scheduleindex == 0)
				schedulereorderup.Enabled = false;
			schedulereorderup.OnClick += delegate {ScheduleReorderUp(scheduleindex);};
			scheduleheader.Add(schedulereorderup);
			var schedulereorderdown = new IconButton("arrow_downward");
			if (scheduleindex == Schedules.Count - 1)
				schedulereorderdown.Enabled = false;
			schedulereorderdown.OnClick += delegate {ScheduleReorderDown(scheduleindex);};
			scheduleheader.Add(schedulereorderdown);
			var scheduleremove = new IconButton("close");
			scheduleremove.OnClick += delegate {ScheduleRemove(scheduleindex);};
			scheduleheader.Add(scheduleremove);

			if (schedule.Collapsed) {
				schedulename.Text += "    " + schedule.Name;
				schedcollapsebutton.Icon = "keyboard_arrow_right";
			} else {
				PageBody.AddSpacingCell(5f);
				var schedulebody = PageBody.AddRow();
				schedulebody.Alignment = TextFlag.Left;
				schedulebody.Spacing = 5f;
				schedulebody.AddSpacingCell(5f);

				var descriptiondescription = new Label("Notes:");
				schedulebody.Add(descriptiondescription);
				var descriptionbox = new LineEdit();
				descriptionbox.Text = schedule.Description;
				descriptionbox.TextChanged += delegate(string newstring) {ScheduleDescriptionChanged(scheduleindex, newstring);};
				schedulebody.Add(descriptionbox);
				schedulebody.AddSpacingCell(5f);

				for (int i2 = 0; i2 < schedule.Tasks.Count; i2++) {
					var taskindex = i2;
					var task = schedule.Tasks[taskindex];

					PageBody.AddSpacingCell(10f);
					var taskheader = PageBody.AddRow();
					taskheader.Alignment = TextFlag.Left;
					taskheader.Spacing = 5f;
					taskheader.AddSpacingCell(20f);

					var taskcollapsebutton = new IconButton("keyboard_arrow_down");
					taskcollapsebutton.OnClick += delegate {TaskToggleCollapsed(scheduleindex, taskindex);};
					taskheader.Add(taskcollapsebutton);
					var taskname = new Label(taskindex + ":");
					taskheader.Add(taskname);

					if (!task.Collapsed) {
						var taskhandlerdropdown = new ComboBox();
						foreach(var taskHandler in TaskHandlers)
							taskhandlerdropdown.AddItem(taskHandler.MethodName, null, delegate {TaskSelectHandler(scheduleindex, taskindex, taskHandler);});
						taskhandlerdropdown.TrySelectNamed(task.TaskHandler.MethodName);
						taskheader.Add(taskhandlerdropdown);
					}
					taskheader.AddStretchCell();

					var taskreorderup = new IconButton("arrow_upward");
					if (taskindex == 0)
						taskreorderup.Enabled = false;
					taskreorderup.OnClick += delegate {TaskReorderUp(scheduleindex, taskindex);};
					taskheader.Add(taskreorderup);
					var taskreorderdown = new IconButton("arrow_downward");
					if (taskindex == schedule.Tasks.Count - 1)
						taskreorderdown.Enabled = false;
					taskreorderdown.OnClick += delegate {TaskReorderDown(scheduleindex, taskindex);};
					taskheader.Add(taskreorderdown);
					var taskremove = new IconButton("close");
					taskremove.OnClick += delegate {TaskRemove(scheduleindex, taskindex);};
					taskheader.Add(taskremove);

					if (task.Collapsed) {
						taskcollapsebutton.Icon = "keyboard_arrow_right";
						taskname.Text += "    " + task.TaskHandler.MethodName;
					} else {
						if (task.HasValidTaskHandler && task.TaskHandler.Parameters.Count > 0) {
							PageBody.AddSpacingCell(5f);
							var parameters = PageBody.AddRow();
							parameters.Alignment = TextFlag.Left;
							parameters.Spacing = 5f;
							parameters.AddSpacingCell(10f);
							foreach (var param in task.TaskHandler.Parameters) {
								parameters.AddSpacingCell(15f);
								var paramlabel = new Label(param.Key.Name + ":");
								parameters.Add(paramlabel);

								if (param.Key.ParameterType == typeof(string)) {
									var paraminput = new LineEdit();
									paraminput.Text = (string)param.Value;
									paraminput.TextChanged += delegate(string newstring) {TaskParameterSet(scheduleindex, taskindex, param.Key, newstring);};
									TaskParameterSet(scheduleindex, taskindex, param.Key, param.Value);
									parameters.Add(paraminput);
								} else if (param.Key.ParameterType == typeof(bool)) {
									var paraminput = new Checkbox();
									if (param.Value != null)
										paraminput.Value = (bool)param.Value;
									paraminput.Toggled += delegate {TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);};
									TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);
									parameters.Add(paraminput);
								} else if (param.Key.ParameterType.IsEnum) {
									var paraminput = new ComboBox();
									foreach (var enumname in param.Key.ParameterType.GetEnumNames())
										paraminput.AddItem(enumname, null, delegate {TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.CurrentText);});
									if (param.Value != null)
										paraminput.TrySelectNamed(param.Value.ToString());
									TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.CurrentText);
									parameters.Add(paraminput);
								} else if (param.Key.ParameterType == typeof(float)) {
									var paraminput = new FloatProperty(null);
									if (param.Value != null)
										paraminput.Value = (float)param.Value;
									paraminput.MinimumWidth = 80f;
									paraminput.OnValueEdited += delegate {TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);};
									TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);
									parameters.Add(paraminput);
								} else if (param.Key.ParameterType == typeof(int)) {
									var paraminput = new IntProperty(null);
									if (param.Value != null)
										paraminput.Value = (int)param.Value;
									paraminput.MinimumWidth = 60f;
									paraminput.OnValueEdited += delegate {TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);};
									TaskParameterSet(scheduleindex, taskindex, param.Key, paraminput.Value);
									parameters.Add(paraminput);
								} else {
									var paraminput = new Label(param.Key.ParameterType.ToString() + " not supported");
									paraminput.Color = Color.Red;
									parameters.Add(paraminput);
								}
							}
							parameters.AddStretchCell();
						}

						PageBody.AddSpacingCell(5f);
						var tasknotes = PageBody.AddRow();
						tasknotes.Alignment = TextFlag.Left;
						tasknotes.Spacing = 5f;
						tasknotes.AddSpacingCell(25f);
						var taskdescriptiondescription = new Label("Notes:");
						tasknotes.Add(taskdescriptiondescription);
						var taskdescriptionbox = new LineEdit();
						taskdescriptionbox.Text = task.Description;
						taskdescriptionbox.TextChanged += delegate(string newstring) {TaskDescriptionChanged(scheduleindex, taskindex, newstring);};
						tasknotes.Add(taskdescriptionbox);
						tasknotes.AddSpacingCell(5f);

						for (int i3 = 0; i3 < task.Conditions.Count; i3++) {
							var condindex = i3;
							var cond = task.Conditions[condindex];

							PageBody.AddSpacingCell(10f);
							var condheader = PageBody.AddRow();
							condheader.Alignment = TextFlag.Left;
							condheader.Spacing = 5f;
							condheader.AddSpacingCell(65f);

							condheader.Add(new Label(condindex.ToString() + ":"));

							var conddropdown = new ComboBox();
							foreach(var condHandler in ConditionHandlers)
								conddropdown.AddItem(condHandler.MethodName, null, delegate {CondSelectHandler(scheduleindex, taskindex, condindex, condHandler);});
							conddropdown.TrySelectNamed(cond.ConditionHandler.MethodName);
							condheader.Add(conddropdown);
							condheader.AddStretchCell();

							var condreorderup = new IconButton("arrow_upward");
							if (condindex == 0)
								condreorderup.Enabled = false;
							condreorderup.OnClick += delegate {CondReorderUp(scheduleindex, taskindex, condindex);};
							condheader.Add(condreorderup);
							var condreorderdown = new IconButton("arrow_downward");
							if (condindex == task.Conditions.Count - 1)
								condreorderdown.Enabled = false;
							condreorderdown.OnClick += delegate {CondReorderDown(scheduleindex, taskindex, condindex);};
							condheader.Add(condreorderdown);
							var condremove = new IconButton("close");
							condremove.OnClick += delegate {CondRemove(scheduleindex, taskindex, condindex);};
							condheader.Add(condremove);

							if (cond.HasValidConditionHandler && cond.ConditionHandler.Parameters.Count > 0) {
								PageBody.AddSpacingCell(5f);
								var parameters = PageBody.AddRow();
								parameters.Alignment = TextFlag.Left;
								parameters.Spacing = 5f;
								parameters.AddSpacingCell(50f);
								foreach (var param in cond.ConditionHandler.Parameters) {
									parameters.AddSpacingCell(15f);
									var paramlabel = new Label(param.Key.Name + ":");
									parameters.Add(paramlabel);

									if (param.Key.ParameterType == typeof(string)) {
										var paraminput = new LineEdit();
										paraminput.Text = (string)param.Value;
										paraminput.TextChanged += delegate(string newstring) {CondParameterSet(scheduleindex, taskindex, condindex, param.Key, newstring);};
										CondParameterSet(scheduleindex, taskindex, condindex, param.Key, param.Value);
										parameters.Add(paraminput);
									} else if (param.Key.ParameterType == typeof(bool)) {
										var paraminput = new Checkbox();
										if (param.Value != null)
											paraminput.Value = (bool)param.Value;
										paraminput.Toggled += delegate {CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);};
										CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);
										parameters.Add(paraminput);
									} else if (param.Key.ParameterType.IsEnum) {
										var paraminput = new ComboBox();
										foreach (var enumname in param.Key.ParameterType.GetEnumNames())
											paraminput.AddItem(enumname, null, delegate {CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.CurrentText);});
										if (param.Value != null)
											paraminput.TrySelectNamed(param.Value.ToString());
										CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.CurrentText);
										parameters.Add(paraminput);
									} else if (param.Key.ParameterType == typeof(float)) {
										var paraminput = new FloatProperty(null);
										if (param.Value != null)
											paraminput.Value = (float)param.Value;
										paraminput.MinimumWidth = 80f;
										paraminput.OnValueEdited += delegate {CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);};
										CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);
										parameters.Add(paraminput);
									} else if (param.Key.ParameterType == typeof(int)) {
										var paraminput = new IntProperty(null);
										if (param.Value != null)
											paraminput.Value = (int)param.Value;
										paraminput.MinimumWidth = 60f;
										paraminput.OnValueEdited += delegate {CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);};
										CondParameterSet(scheduleindex, taskindex, condindex, param.Key, paraminput.Value);
										parameters.Add(paraminput);
									} else {
										var paraminput = new Label(param.Key.ParameterType.ToString() + " not supported");
										paraminput.Color = Color.Red;
										parameters.Add(paraminput);
									}
								}
								parameters.AddStretchCell();
							}
						}

						PageBody.AddSpacingCell(5f);
						var newcondbuttonrow = PageBody.AddRow();
						newcondbuttonrow.Alignment = TextFlag.Left;
						newcondbuttonrow.Spacing = 5f;
						newcondbuttonrow.AddSpacingCell(60f);
						var newcondbutton = new IconButton("+");
						newcondbutton.OnClick += delegate {AddNewCondition(scheduleindex, taskindex);};
						newcondbuttonrow.Add(newcondbutton);
						PageBody.AddSpacingCell(5f);
					}
				}

				PageBody.AddSpacingCell(5f);
				var newtaskbuttonrow = PageBody.AddRow();
				newtaskbuttonrow.Alignment = TextFlag.Left;
				newtaskbuttonrow.Spacing = 5f;
				newtaskbuttonrow.AddSpacingCell(20f);
				var newtaskbutton = new IconButton("+");
				newtaskbutton.OnClick += delegate {AddNewTask(scheduleindex);};
				newtaskbuttonrow.Add(newtaskbutton);
			}
			PageBody.AddSpacingCell(5f);
		}

		PageBody.AddSpacingCell(5f);
		var addnewbottom = PageBody.AddRow();
		addnewbottom.Alignment = TextFlag.CenterHorizontally;
		var newbutton = new IconButton("+");
		newbutton.OnClick += delegate {AddNewSchedule();};
		addnewbottom.Add(newbutton);
		addnewbottom.AddStretchCell();

		Layout.Add(scroll);

		Update();
	}

	public void AddNewSchedule() {
		Schedules.Add(new ScheduleInfo());
		DrawUI();
	}
	public void ScheduleToggleCollapsed(int schedule) {
		var schedInfo = Schedules[schedule];
		schedInfo.Collapsed = !schedInfo.Collapsed;
		Schedules[schedule] = schedInfo;
		DrawUI();
	}
	public void ScheduleReorderUp(int schedule) {
		var schedInfo = Schedules[schedule];
		Schedules.RemoveAt(schedule);
		Schedules.Insert(schedule - 1, schedInfo);
		DrawUI();
	}
	public void ScheduleReorderDown(int schedule) {
		var schedInfo = Schedules[schedule];
		Schedules.RemoveAt(schedule);
		Schedules.Insert(schedule + 1, schedInfo);
		DrawUI();
	}
	public void ScheduleRemove(int schedule) {
		Schedules.RemoveAt(schedule);
		DrawUI();
	}
	public void ScheduleNameChanged(int schedule, string description) {
		var schedInfo = Schedules[schedule];
		schedInfo.Name = description;
		Schedules[schedule] = schedInfo;
	}
	public void ScheduleDescriptionChanged(int schedule, string description) {
		var schedInfo = Schedules[schedule];
		schedInfo.Description = description;
		Schedules[schedule] = schedInfo;
	}
	public void AddNewTask(int schedule) {
		var schedInfo = Schedules[schedule];
		var taskinfo = new TaskInfo();
		taskinfo.TaskHandler = TaskHandlers.First();
		schedInfo.Tasks.Add(taskinfo);
		Schedules[schedule] = schedInfo;
		DrawUI();
	}
	public void TaskToggleCollapsed(int schedule, int task) {
		var TaskInfo = Schedules[schedule].Tasks[task];
		TaskInfo.Collapsed = !TaskInfo.Collapsed;
		Schedules[schedule].Tasks[task] = TaskInfo;
		DrawUI();
	}
	public void TaskReorderUp(int schedule, int task) {
		var taskInfo = Schedules[schedule].Tasks[task];
		Schedules[schedule].Tasks.RemoveAt(task);
		Schedules[schedule].Tasks.Insert(task - 1, taskInfo);
		DrawUI();
	}
	public void TaskReorderDown(int schedule, int task) {
		var taskInfo = Schedules[schedule].Tasks[task];
		Schedules[schedule].Tasks.RemoveAt(task);
		Schedules[schedule].Tasks.Insert(task + 1, taskInfo);
		DrawUI();
	}
	public void TaskRemove(int schedule, int task) {
		Schedules[schedule].Tasks.RemoveAt(task);
		DrawUI();
	}
	public void TaskSelectHandler(int schedule, int task, HandlerInfo handler) {
		//copy the handler so data doesnt do weird shit
		var handlercopy = new HandlerInfo();
		handlercopy.MethodName = handler.MethodName;
		handlercopy.Parameters = new Dictionary<ParameterInfo, object>();
		foreach(var param in handler.Parameters)
			handlercopy.Parameters.Add(param.Key, param.Value);
		handler = handlercopy;

		var TaskInfo = Schedules[schedule].Tasks[task];
		var newhandler = TaskInfo.TaskHandler.MethodName != handler.MethodName;
		if (newhandler) { //save the old handler info and try to restore for new handler
			TaskInfo.PreviousTaskHandlers.Add(TaskInfo.TaskHandler);
			foreach (var oldhandler in TaskInfo.PreviousTaskHandlers) {
				if (oldhandler.MethodName == handler.MethodName)
					handler = oldhandler;
			}
			TaskInfo.PreviousTaskHandlers.RemoveAll(oldhandler => oldhandler.MethodName == handler.MethodName);
		}
		TaskInfo.TaskHandler = handler;
		TaskInfo.HasValidTaskHandler = true;
		Schedules[schedule].Tasks[task] = TaskInfo;
		if (newhandler)
			DrawUI();
	}
	public void TaskDescriptionChanged(int schedule, int task, string newstring) {
		var TaskInfo = Schedules[schedule].Tasks[task];
		TaskInfo.Description = newstring;
		Schedules[schedule].Tasks[task] = TaskInfo;
	}
	public void TaskParameterSet(int schedule, int task, ParameterInfo param, object newValue) {
		var TaskInfo = Schedules[schedule].Tasks[task];
		TaskInfo.TaskHandler.Parameters[param] = newValue;
		Schedules[schedule].Tasks[task] = TaskInfo;
	}
	public void AddNewCondition(int schedule, int task) {
		var taskInfo = Schedules[schedule].Tasks[task];
		taskInfo.Conditions.Add(new ConditionInfo());
		Schedules[schedule].Tasks[task] = taskInfo;
		DrawUI();
	}
	public void CondSelectHandler(int schedule, int task, int cond, HandlerInfo handler) {
		//copy the handler so data doesnt do weird shit
		var handlercopy = new HandlerInfo();
		handlercopy.MethodName = handler.MethodName;
		handlercopy.Parameters = new Dictionary<ParameterInfo, object>();
		foreach(var param in handler.Parameters)
			handlercopy.Parameters.Add(param.Key, param.Value);
		handler = handlercopy;

		var condInfo = Schedules[schedule].Tasks[task].Conditions[cond];
		var newhandler = condInfo.ConditionHandler.MethodName != handler.MethodName;
		if (newhandler) { //save the old handler info and try to restore for new handler
			condInfo.PreviousConditionHandlers.Add(condInfo.ConditionHandler);
			foreach (var oldhandler in condInfo.PreviousConditionHandlers) {
				if (oldhandler.MethodName == handler.MethodName)
					handler = oldhandler;
			}
			condInfo.PreviousConditionHandlers.RemoveAll(oldhandler => oldhandler.MethodName == handler.MethodName);
		}
		condInfo.ConditionHandler = handler;
		condInfo.HasValidConditionHandler = true;
		Schedules[schedule].Tasks[task].Conditions[cond] = condInfo;
		if (newhandler)
			DrawUI();
	}
	public void CondReorderUp(int schedule, int task, int cond) {
		var condInfo = Schedules[schedule].Tasks[task].Conditions[cond];
		Schedules[schedule].Tasks[task].Conditions.RemoveAt(cond);
		Schedules[schedule].Tasks[task].Conditions.Insert(cond - 1, condInfo);
		DrawUI();
	}
	public void CondReorderDown(int schedule, int task, int cond) {
		var condInfo = Schedules[schedule].Tasks[task].Conditions[cond];
		Schedules[schedule].Tasks[task].Conditions.RemoveAt(cond);
		Schedules[schedule].Tasks[task].Conditions.Insert(cond + 1, condInfo);
		DrawUI();
	}
	public void CondRemove(int schedule, int task, int cond) {
		Schedules[schedule].Tasks[task].Conditions.RemoveAt(cond);
		DrawUI();
	}
	public void CondParameterSet(int schedule, int task, int cond, ParameterInfo param, object newValue) {
		var condInfo = Schedules[schedule].Tasks[task].Conditions[cond];
		condInfo.ConditionHandler.Parameters[param] = newValue;
		Schedules[schedule].Tasks[task].Conditions[cond] = condInfo;
	}

	private void UpdateTaskhandlerList() {
		TaskHandlers = new List<HandlerInfo>();
		foreach (var method in typeof(NpcTaskHandlers).GetMethods()) {
			if (method.Name.StartsWith("TASK_")) {
				var handlerinfo = new HandlerInfo();
				handlerinfo.MethodName = method.Name;
				handlerinfo.Parameters = new Dictionary<ParameterInfo, object>();
				foreach(var parameter in method.GetParameters()) {
					if (parameter.Name.ToLower() == "owner" && parameter.ParameterType == typeof(BaseNpc))
						continue;
					var def = parameter.DefaultValue;
					if (def.GetType() == typeof(DBNull)) //idk what dbnull is but sounds lame
						def = null;
					handlerinfo.Parameters.Add(parameter, def);
				}
				TaskHandlers.Add(handlerinfo);
			}
		}
	}
	public void UpdateConditionHandlerList() {
		ConditionHandlers = new List<HandlerInfo>();
		foreach (var method in typeof(NpcConditionHandlers).GetMethods()) {
			if (method.Name.StartsWith("COND_")) {
				var handlerinfo = new HandlerInfo();
				handlerinfo.MethodName = method.Name;
				handlerinfo.Parameters = new Dictionary<ParameterInfo, object>();
				foreach(var parameter in method.GetParameters()) {
					if (parameter.Name.ToLower() == "owner" && parameter.ParameterType == typeof(BaseNpc))
						continue;
					var def = parameter.DefaultValue;
					if (def.GetType() == typeof(DBNull)) //idk what dbnull is but sounds lame
						def = null;
					handlerinfo.Parameters.Add(parameter, def);
				}
				ConditionHandlers.Add(handlerinfo);
			}
		}
	}

	public bool AssetSave(bool saveAs) {
		var savePath = CurrentFile == null || saveAs ? GetSavePath() : CurrentFile.AbsolutePath;
		if (string.IsNullOrWhiteSpace(savePath))
			return false;

		//serialize
		var file = new JsonArray();
		foreach (var schedule in Schedules) {
			var tasks = new JsonArray();
			foreach (var task in schedule.Tasks) {
				var handler = new JsonObject();
				if (task.HasValidTaskHandler) {
					var parameters = new JsonArray();
					foreach (var parameter in task.TaskHandler.Parameters) {
						parameters.Add(new JsonObject(){
							{"parameterName", parameter.Key.Name},
							{"parameterType", parameter.Key.ParameterType.Name},
							{"parameterIsEnum", parameter.Key.ParameterType.IsEnum},
							{"value", parameter.Value.ToString()}
						});
					}
					handler = new JsonObject{
						{"methodName", task.TaskHandler.MethodName},
						{"parameters", parameters}
					};
				}
				var conditions = new JsonArray();
				foreach (var condition in task.Conditions) {
					var condhandler = new JsonObject();
					if (condition.HasValidConditionHandler) {
						var parameters = new JsonArray();
						foreach (var parameter in condition.ConditionHandler.Parameters) {
							parameters.Add(new JsonObject(){
								{"parameterName", parameter.Key.Name},
								{"parameterType", parameter.Key.ParameterType.Name},
								{"parameterIsEnum", parameter.Key.ParameterType.IsEnum},
								{"value", parameter.Value.ToString()}
							});
						}
						condhandler = new JsonObject{
							{"methodName", task.TaskHandler.MethodName},
							{"parameters", parameters}
						};
					}
					conditions.Add(new JsonObject(){
						{"conditionHandler", condhandler},
					});
				}
				tasks.Add(new JsonObject{
					{"description", task.Description},
					{"collapsed", task.Collapsed},
					{"taskHandler", handler},
					{"conditions", conditions}
				});
			}

			file.Add(new JsonObject{
				{"name", schedule.Name},
				{"description", schedule.Description},
				{"collapsed", schedule.Collapsed},
				{"tasks", tasks}
			});
		}
		System.IO.File.WriteAllText(savePath, file.ToString());

		if (saveAs)
			CurrentFile = null;

		CurrentFile ??= AssetSystem.RegisterFile(savePath);
		MainAssetBrowser.Instance?.UpdateAssetList();

		Dirty = false;
		DrawUI(false);

		return true;
	}
	private static string GetSavePath() {
		var fd = new FileDialog(null) {
			Title = $"Save Schedule Set",
			DefaultSuffix = $".sched"
		};

		fd.SelectFile($"untitled.sched");
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter("Schedule Set (*.sched)");
		if (!fd.Execute())
			return null;

		return fd.SelectedFile;
	}

	public void AssetOpen(Asset asset) {
		return;
	}
	public void SelectMember(string s) {}
}
