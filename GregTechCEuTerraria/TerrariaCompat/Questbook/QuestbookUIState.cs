#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Config;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.TerrariaCompat.UI;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.Questbook;

public sealed class QuestbookUIState : FreeModalWindow
{
	private UITerrariaPanel _panel = null!;
	private UIList _chapterList = null!;
	private UIScrollbar _chapterBar = null!;
	private QuestGraph _graph = null!;
	private UITerrariaPanel _questPanel = null!;
	private UIList _detailList = null!;
	private UIScrollbar _detailBar = null!;
	private bool _built;

	private UIText _title = null!;
	private UIText _editWarning = null!;
	private UITextButton _btnAddQuest = null!, _btnExportBook = null!, _btnExportChapter = null!;
	private UITextButton _editToggle = null!;
	private bool _lastEditMode;

	private bool _questOpen;
	private bool _questAttached;

	private int _chapterIndex = -1;
	private string? _selectedQuest;

	public string? SelectedQuestId => _selectedQuest;

	private const int Pad = 8;
	private const int TitleH = 28;
	private const int BarW = 20;
	private const int ChapterW = 232;
	private const int MoveKnobW = 20;
	private const int ResizeKnobSz = 28;

	protected override void RebuildWindow()
	{
		var root = RootSize();
		ResolveSize(root.X, root.Y, 1200f, 720f, MinModalW, MinModalH);
		float w = CurW, h = CurH;

		if (!_built) BuildStructure();

		_panel.Width  = StyleDimension.FromPixels(w);
		_panel.Height = StyleDimension.FromPixels(h);

		int contentTop = Pad + TitleH + 6;
		int contentH   = (int)h - contentTop - Pad;
		int rightLeft  = Pad + ChapterW + BarW + 12;
		int rightW     = (int)w - rightLeft - Pad;
		int toolH      = QuestbookEditor.Enabled ? 50 : 0;
		int graphTop   = contentTop + toolH;
		int graphH     = contentH - toolH;

		PositionList(_chapterList, _chapterBar, Pad, contentTop, ChapterW, contentH);
		PositionToolbar(rightLeft, contentTop, rightW);
		_graph.Left   = StyleDimension.FromPixels(rightLeft);
		_graph.Top    = StyleDimension.FromPixels(graphTop);
		_graph.Width  = StyleDimension.FromPixels(rightW);
		_graph.Height = StyleDimension.FromPixels(graphH);
		if (_questAttached) LayoutQuestPanel();

		LayoutHeaderButtons(_panel, w, Pad, TitleH);
		_editToggle.Left = StyleDimension.FromPixels(HeaderButtonsLeft(w, Pad) - 6 - 46);
		LayoutResizeKnob(_panel, w, h, ResizeKnobSz, "Drag to resize the questbook");
		ApplyCenteredMoveClamp(_panel, root, w, h);

		Recalculate();

		if (!_built)
		{
			_built = true;
			BuildChapters();
			if (QuestbookSystem.Data.Chapters.Count > 0)
				SelectChapter(0);
		}
	}

	private void BuildStructure()
	{
		_panel = new UITerrariaPanel { HAlign = 0.5f, VAlign = 0.5f };
		Append(_panel);

		var moveKnob = NewMoveKnob("Drag to move the questbook");
		moveKnob.Left   = StyleDimension.FromPixels(Pad);
		moveKnob.Top    = StyleDimension.FromPixels(Pad);
		moveKnob.Width  = StyleDimension.FromPixels(MoveKnobW);
		moveKnob.Height = StyleDimension.FromPixels(TitleH);
		_panel.Append(moveKnob);

		_title = new UIText("GregTech Quests", 1.05f)
		{
			Left = StyleDimension.FromPixels(Pad + MoveKnobW + 6),
			Top  = StyleDimension.FromPixels(Pad),
			IgnoresMouseInteraction = true,
		};
		_panel.Append(_title);

		var warning = new UIText("WIP - new questbook ETA v0.0.15", 1.0f)
		{
			HAlign = 0.5f,
			Top = StyleDimension.FromPixels(Pad),
			TextColor = Color.Red,
			IgnoresMouseInteraction = true,
		};
		_panel.Append(warning);

		_editToggle = new UITextButton(
			() => "Edit",
			() =>
			{
				GTClientConfig.Instance.QuestbookEditMode = !GTClientConfig.Instance.QuestbookEditMode;
				GTClientConfig.Instance.Persist();
			},
			tooltip: "Toggle edit mode (preview how the questbook looks to players)",
			width: 46, height: 20)
		{
			Top = StyleDimension.FromPixels(Pad + 2),
			IsActive = () => QuestbookEditor.Enabled,
		};
		_panel.Append(_editToggle);

		_chapterList = MakeList(out _chapterBar);
		_chapterList.ManualSortMethod = items => items.Sort(
			(a, b) => ((a as ChapterRow)?.Index ?? 0) - ((b as ChapterRow)?.Index ?? 0));
		_panel.Append(_chapterList);
		_panel.Append(_chapterBar);

		_graph = new QuestGraph(this);
		_panel.Append(_graph);

		_questPanel = new UITerrariaPanel
		{
			BackgroundColor = new Color(34, 37, 60),
			BorderColor = new Color(120, 135, 215),
			OverflowHidden = true,
		};
		_detailList = MakeList(out _detailBar);
		_detailList.ListPadding = 6f;
		_detailList.ManualSortMethod = items => { };
		_questPanel.Append(_detailList);
		_questPanel.Append(_detailBar);
		var qClose = new UIText("X", 1.0f)
		{
			HAlign = 1f,
			Left = StyleDimension.FromPixels(-6),
			Top  = StyleDimension.FromPixels(5),
		};
		qClose.OnLeftClick += (_, _) => CloseQuestPanel();
		_questPanel.Append(qClose);

		BuildToolbar();
	}

	private void BuildToolbar()
	{
		System.Func<bool> shown = () => QuestbookEditor.Enabled;

		_btnAddQuest = new UITextButton(() => "+ Quest", AddQuestHere,
			tooltip: "Add a new quest at the graph centre") { IsVisible = shown };
		_btnExportBook = new UITextButton(() => "Export book to clipboard",
			() => QuestbookEditor.ExportBookToClipboard(),
			tooltip: "Copy the whole questbook JSON to the clipboard") { IsVisible = shown };
		_btnExportChapter = new UITextButton(() => "Export chapter to clipboard",
			ExportChapterHere,
			tooltip: "Copy this chapter + its quests JSON to the clipboard") { IsVisible = shown };

		_panel.Append(_btnAddQuest);
		_panel.Append(_btnExportBook);
		_panel.Append(_btnExportChapter);

		_editWarning = new UIText("", 0.82f) { TextColor = new Color(255, 120, 120) };
		_panel.Append(_editWarning);
	}

	private void PositionToolbar(int left, int top, int width)
	{
		int x = left, h = 24, gap = 4;
		void Place(UITextButton b, int w)
		{
			b.Left   = StyleDimension.FromPixels(x);
			b.Top    = StyleDimension.FromPixels(top);
			b.Width  = StyleDimension.FromPixels(w);
			b.Height = StyleDimension.FromPixels(h);
			x += w + gap;
		}
		Place(_btnAddQuest, 78);
		Place(_btnExportBook, 168);
		Place(_btnExportChapter, 182);

		_editWarning.Left = StyleDimension.FromPixels(left);
		_editWarning.Top  = StyleDimension.FromPixels(top + h + 2);
	}

	private void ExportChapterHere()
	{
		if (_chapterIndex >= 0 && _chapterIndex < QuestbookSystem.Data.Chapters.Count)
			QuestbookEditor.ExportChapterToClipboard(QuestbookSystem.Data.Chapters[_chapterIndex]);
	}

	private void AddQuestHere()
	{
		if (_chapterIndex < 0 || _chapterIndex >= QuestbookSystem.Data.Chapters.Count)
			return;
		ChapterData chapter = QuestbookSystem.Data.Chapters[_chapterIndex];
		(float gx, float gy) = _graph.ViewCenterGrid();
		QuestData q = QuestbookEditor.AddQuest(chapter, gx, gy);
		_graph.RefreshIndex();
		SelectQuest(q);
	}

	protected override void OnModalUpdate(GameTime gameTime)
	{
		bool edit = QuestbookEditor.Enabled;
		if (edit != _lastEditMode)
		{
			_lastEditMode = edit;
			if (_built) RebuildWindow();
			if (_selectedQuest != null
				&& QuestbookSystem.QuestsById.TryGetValue(_selectedQuest, out QuestData? q))
				SelectQuest(q);
		}

		_title.SetText(edit
			? (QuestbookEditor.Dirty ? "GregTech Quests (edit*)" : "GregTech Quests (edit)")
			: "GregTech Quests");
		_editWarning.SetText(edit
			? "Changes aren't saved anywhere, export to clipboard before closing the game!"
			: "");

		if (QuestbookEditor.AwaitingDep && !QuestbookEditor.DepPickArmed && !Main.mouseLeft)
			QuestbookEditor.ArmDepPick();

		if (_questOpen && !_questAttached)
		{
			_panel.Append(_questPanel);
			_questAttached = true;
			LayoutQuestPanel();
			Recalculate();
		}
		else if (!_questOpen && _questAttached)
		{
			_panel.RemoveChild(_questPanel);
			_questAttached = false;
		}

		if (_questAttached && !ItemPickerSystem.IsOpen
			&& MouseClick.LeftPressed)
		{
			var r = _questPanel.GetDimensions().ToRectangle();
			if (!r.Contains(ModalEscape.PollCursor()))
				CloseQuestPanel();
		}
	}

	private void LayoutQuestPanel()
	{
		float w = CurW, h = CurH;
		int contentTop = Pad + TitleH + 6;
		int contentH   = (int)h - contentTop - Pad;
		int rightLeft  = Pad + ChapterW + BarW + 12;
		int rightW     = (int)w - rightLeft - Pad;
		int toolH      = QuestbookEditor.Enabled ? 50 : 0;
		int graphTop   = contentTop + toolH;
		int graphH     = contentH - toolH;

		int panelW = Math.Max(220, (int)(rightW * 0.82f));
		int panelH = Math.Max(180, (int)(graphH * 0.82f));
		int panelLeft = rightLeft + (rightW - panelW) / 2;
		int panelTop  = graphTop + (graphH - panelH) / 2;

		_questPanel.Left   = StyleDimension.FromPixels(panelLeft);
		_questPanel.Top    = StyleDimension.FromPixels(panelTop);
		_questPanel.Width  = StyleDimension.FromPixels(panelW);
		_questPanel.Height = StyleDimension.FromPixels(panelH);

		const int innerTop = 24;
		PositionList(_detailList, _detailBar, Pad, innerTop, panelW - Pad * 2, panelH - innerTop - Pad);
	}

	internal bool IsQuestPanelOpen => _questOpen;

	internal void CloseQuestPanel()
	{
		if (!_questOpen) return;
		_questOpen = false;
		_selectedQuest = null;
		UITextField.UnfocusAll();
	}

	internal bool IsPointerOverQuestPanel()
		=> _questAttached
		&& _questPanel.GetDimensions().ToRectangle().Contains(ModalEscape.PollCursor());

	internal void OnChapterDeleted()
	{
		CloseQuestPanel();
		BuildChapters();
		int count = QuestbookSystem.Data.Chapters.Count;
		if (count == 0) { _chapterIndex = -1; _graph.LoadChapter(new ChapterData()); return; }
		SelectChapter(System.Math.Min(_chapterIndex, count - 1));
	}

	protected override void ApplyOffsetLive()
	{
		if (_panel is null) return;
		_panel.Left = StyleDimension.FromPixels(OffsetX);
		_panel.Top  = StyleDimension.FromPixels(OffsetY);
		Recalculate();
	}

	private static UIList MakeList(out UIScrollbar bar)
	{
		var list = new UIList { ListPadding = 2f };
		bar = new UIScrollbar();
		list.SetScrollbar(bar);
		return list;
	}

	private static void PositionList(UIList list, UIScrollbar bar, int left, int top, int width, int height)
	{
		list.Left   = StyleDimension.FromPixels(left);
		list.Top    = StyleDimension.FromPixels(top);
		list.Width  = StyleDimension.FromPixels(width - BarW - 4);
		list.Height = StyleDimension.FromPixels(height);
		bar.Left    = StyleDimension.FromPixels(left + width - BarW);
		bar.Top     = StyleDimension.FromPixels(top);
		bar.Width   = StyleDimension.FromPixels(BarW);
		bar.Height  = StyleDimension.FromPixels(height);
	}

	private void BuildChapters()
	{
		_chapterList.Clear();
		for (int i = 0; i < QuestbookSystem.Data.Chapters.Count; i++)
			_chapterList.Add(new ChapterRow(this, i));
	}

	public void SelectChapter(int index)
	{
		CloseQuestPanel();
		_chapterIndex = index;
		_selectedQuest = null;

		if (index < 0 || index >= QuestbookSystem.Data.Chapters.Count)
			return;

		_graph.LoadChapter(QuestbookSystem.Data.Chapters[index]);
	}

	internal void SelectQuest(QuestData quest)
	{
		_selectedQuest = quest.Id;
		_questOpen = true;
		_detailList.Clear();

		if (QuestbookEditor.Enabled)
		{
			BuildQuestEditor(quest);
			return;
		}

		_detailList.Add(new WrappedText(quest.Title, 0.95f, Color.White));

		if (!string.IsNullOrEmpty(quest.Subtitle))
			_detailList.Add(new WrappedText(quest.Subtitle, 0.72f, new Color(190, 190, 210)));

		if (!string.IsNullOrEmpty(quest.Desc))
			_detailList.Add(new WrappedText(quest.Desc, 0.78f, new Color(215, 215, 215)));

		if (QuestbookSystem.Resolved.TryGetValue(quest.Id, out ResolvedQuest? resolved))
		{
			for (int i = 0; i < resolved.Tasks.Count; i++)
				_detailList.Add(new TaskLine(quest.Id, i, resolved.Tasks[i]));

			if (!resolved.AutoCheck)
				_detailList.Add(new CompleteButton(quest.Id));
		}
	}

	private void BuildQuestEditor(QuestData quest)
	{
		_detailList.Add(FullWidthButton("Export quest to clipboard", 24,
			() => QuestbookEditor.ExportQuestToClipboard(quest)));
		_detailList.Add(new WrappedText(string.IsNullOrEmpty(quest.Title) ? "(untitled quest)" : quest.Title,
			0.92f, Color.White));
		_detailList.Add(new WrappedText(quest.Id, 0.6f, new Color(150, 150, 175)));

		_detailList.Add(new SectionHeader("Quest"));
		AddEditField("Title", () => quest.Title, v => QuestbookEditor.SetField(quest, q => q.Title = v), 64);
		AddEditField("Subtitle", () => quest.Subtitle, v => QuestbookEditor.SetField(quest, q => q.Subtitle = v), 96);

		_detailList.Add(new WrappedText("Description", 0.7f, new Color(185, 192, 220)));
		_detailList.Add(new UITextArea(() => quest.Desc,
			v => QuestbookEditor.SetField(quest, q => q.Desc = v), maxLength: 1024)
		{
			Width  = StyleDimension.Fill,
			Height = StyleDimension.FromPixels(118),
		});

		_detailList.Add(new LabeledControl("Icon", new IconPickControl(
			() => quest.Icon,
			type =>
			{
				string id = IngredientResolverImpl.StableItemId(type);
				QuestbookEditor.SetField(quest, q => q.Icon = id);
				RefreshDetail(quest);
			})));
		_detailList.Add(new LabeledControl("Size", new UITextButton(
			() => $"{quest.Size:0.0}x   (L +  /  R -)",
			() => QuestbookEditor.SetField(quest, q => q.Size = System.Math.Min(4f, q.Size + 0.5f)),
			() => QuestbookEditor.SetField(quest, q => q.Size = System.Math.Max(0.5f, q.Size - 0.5f)),
			tooltip: "Node render size on the graph")));

		_detailList.Add(new SectionHeader("Tasks"));
		foreach (TaskData task in quest.Tasks)
			_detailList.Add(new TaskEditRow(this, quest, task));
		_detailList.Add(FullWidthButton("+ Add Task", 24,
			() => { QuestbookEditor.AddTask(quest); SelectQuest(quest); }));

		_detailList.Add(new SectionHeader("Dependencies (prerequisites)"));
		foreach (string dep in quest.Deps)
			_detailList.Add(new DepEditRow(this, quest, dep));
		_detailList.Add(new UITextButton(
			() => QuestbookEditor.AwaitingDep && QuestbookEditor.DepTarget == quest.Id
				? "Click a prerequisite on the graph... (R: cancel)"
				: "+ Add Dependency",
			() => { QuestbookEditor.StartAddDep(quest.Id); CloseQuestPanel(); },
			() => QuestbookEditor.CancelAddDep(),
			tooltip: "Closes this panel - then click the prerequisite quest in the graph", height: 24)
		{ Width = StyleDimension.Fill, IsActive = () => QuestbookEditor.AwaitingDep && QuestbookEditor.DepTarget == quest.Id });

		_detailList.Add(new SectionHeader(""));
		_detailList.Add(FullWidthButton("Delete Quest", 24, () =>
		{
			QuestbookEditor.DeleteQuest(quest.Id);
			_graph.RefreshIndex();
			CloseQuestPanel();
		}));
	}

	private void AddEditField(string label, System.Func<string> get, System.Action<string> set,
		int max, string placeholder = "")
		=> _detailList.Add(new LabeledControl(label,
			new UITextField(get, set, maxLength: max, placeholder: placeholder)));

	private static UITextButton FullWidthButton(string label, int height, System.Action onClick)
		=> new(() => label, onClick, height: height) { Width = StyleDimension.Fill };

	internal void RefreshDetail(QuestData quest) => SelectQuest(quest);

	private sealed class LabeledControl : UIElement
	{
		private const int LabelW = 86;
		private readonly string _label;
		private readonly UIElement _control;

		public LabeledControl(string label, UIElement control)
		{
			_label = label;
			_control = control;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(26);
			Append(_control);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float w = GetInnerDimensions().Width;
			if (w <= 0) return;
			_control.Left   = StyleDimension.FromPixels(LabelW);
			_control.Top    = StyleDimension.FromPixels(2);
			_control.Width  = StyleDimension.FromPixels(System.Math.Max(40, (int)w - LabelW));
			_control.Height = StyleDimension.FromPixels(22);
			_control.Recalculate();
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			Terraria.Utils.DrawBorderString(sb, _label,
				new Vector2(d.X + 2, d.Y + 6), new Color(185, 192, 220), 0.72f);
		}
	}

	private sealed class IconPickControl : UIElement
	{
		private const int IconSize = 20;
		private readonly Func<string> _get;
		private readonly UITextButton _pick;

		public IconPickControl(Func<string> get, Action<int> onPick)
		{
			_get = get;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(22);
			_pick = new UITextButton(
				() => { string id = _get(); return string.IsNullOrEmpty(id) ? "(pick item)" : id; },
				() => ItemPickerSystem.Open(onPick),
				tooltip: "Search for an item");
			Append(_pick);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float w = GetInnerDimensions().Width;
			if (w <= 0) return;
			int pickX = IconSize + 6;
			_pick.Left   = StyleDimension.FromPixels(pickX);
			_pick.Top    = StyleDimension.FromPixels(0);
			_pick.Width  = StyleDimension.FromPixels(System.Math.Max(40, (int)w - pickX));
			_pick.Height = StyleDimension.FromPixels(22);
			_pick.Recalculate();
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			int type = IngredientResolverImpl.Instance.ResolveItemType(_get());
			if (type <= 0) return;
			CalculatedStyle d = GetDimensions();
			var box = new Rectangle((int)d.X, (int)d.Y + 1, IconSize, IconSize);
			QuestbookIcon.Draw(sb, type, box.Center.ToVector2(), IconSize);
		}
	}

	private sealed class SectionHeader : UIElement
	{
		private readonly string _text;

		public SectionHeader(string text)
		{
			_text = text;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(string.IsNullOrEmpty(text) ? 10 : 24);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			Texture2D px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, new Rectangle((int)d.X, (int)(d.Y + d.Height - 1), (int)d.Width, 1),
				new Color(90, 100, 150));
			if (!string.IsNullOrEmpty(_text))
				Terraria.Utils.DrawBorderString(sb, _text,
					new Vector2(d.X + 2, d.Y + 3), new Color(220, 225, 240), 0.82f);
		}
	}

	private sealed class ChapterRow : UIElement
	{
		private readonly QuestbookUIState _owner;
		private readonly int _index;

		internal int Index => _index;

		public ChapterRow(QuestbookUIState owner, int index)
		{
			_owner = owner;
			_index = index;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(26);
			OnLeftClick += (evt, _) =>
			{
				if (QuestbookEditor.Enabled)
				{
					CalculatedStyle d = GetDimensions();
					if (evt.MousePosition.X >= d.X + d.Width - 22
						&& _index >= 0 && _index < QuestbookSystem.Data.Chapters.Count)
					{
						QuestbookEditor.DeleteChapter(QuestbookSystem.Data.Chapters[_index]);
						_owner.OnChapterDeleted();
						return;
					}
				}
				_owner.SelectChapter(_index);
			};
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			ChapterData chapter = QuestbookSystem.Data.Chapters[_index];
			CalculatedStyle d = GetDimensions();

			bool selected = _owner._chapterIndex == _index;
			if (selected)
				sb.Draw(TextureAssets.MagicPixel.Value, d.ToRectangle(), new Color(120, 130, 200) * 0.35f);
			else if (IsMouseHovering)
				sb.Draw(TextureAssets.MagicPixel.Value, d.ToRectangle(), Color.White * 0.10f);

			int done = 0;
			foreach (NodeData n in chapter.Nodes)
				if (QuestbookProgress.IsComplete(n.Quest))
					done++;
			int total = chapter.Nodes.Count;

			string name = chapter.Title;
			var color = done >= total && total > 0
				? new Color(120, 230, 120)
				: Color.White;
			Terraria.Utils.DrawBorderString(sb, name, new Vector2(d.X + 6, d.Y + 5), color, 0.78f);
			Terraria.Utils.DrawBorderString(sb, $"{done}/{total}",
				new Vector2(d.X + d.Width - 52, d.Y + 5), new Color(180, 180, 195), 0.72f);

			if (QuestbookEditor.Enabled)
				Terraria.Utils.DrawBorderString(sb, "x",
					new Vector2(d.X + d.Width - 16, d.Y + 4), new Color(235, 120, 120), 0.9f);
		}
	}

	private sealed class WrappedText : UIElement
	{
		private readonly string _text;
		private readonly float _scale;
		private readonly Color _color;

		public WrappedText(string text, float scale, Color color)
		{
			_text = text;
			_scale = scale;
			_color = color;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(20);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float width = GetInnerDimensions().Width;
			if (width <= 0)
				return;
			string wrapped = WrapFor(width);
			float h = FontAssets.MouseText.Value.MeasureString(wrapped).Y * _scale;
			Height = StyleDimension.FromPixels(h + 8f);
		}

		private string WrapFor(float width)
			=> FontAssets.MouseText.Value.CreateWrappedText(_text, width / _scale);

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			Terraria.Utils.DrawBorderString(sb, WrapFor(d.Width),
				new Vector2(d.X + 4, d.Y + 2), _color, _scale);
		}
	}

	private sealed class TaskLine : UIElement
	{
		private readonly string _questId;
		private readonly int _index;
		private readonly ResolvedTask _task;

		public TaskLine(string questId, int index, ResolvedTask task)
		{
			_questId = questId;
			_index = index;
			_task = task;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(26);
			OnLeftClick += (evt, _) =>
			{
				if (!_task.IsFluid
					|| QuestbookProgress.IsComplete(_questId)
					|| QuestbookProgress.IsTaskSatisfied(_questId, _index))
					return;
				if (SkipBox().Contains(evt.MousePosition.ToPoint()))
					QuestbookProgress.MarkTask(_questId, _index);
			};
		}

		private int IconType => _task.AcceptTypes.Length > 0 ? _task.AcceptTypes[0] : 0;

		private Rectangle SkipBox()
		{
			CalculatedStyle d = GetDimensions();
			return new Rectangle((int)d.X + (int)d.Width - 78, (int)d.Y + 4, 74, 18);
		}

		private Rectangle IconBox()
		{
			CalculatedStyle d = GetDimensions();
			return new Rectangle((int)d.X + 4, (int)d.Y + 3, 20, 20);
		}

		private string ItemLabel()
			=> !string.IsNullOrEmpty(_task.Label)
				? _task.Label
				: (IconType > 0 ? Lang.GetItemNameValue(IconType) : "");

		private HashSet<int> TagMembers()
		{
			var set = new HashSet<int>();
			foreach (int t in _task.AcceptTypes)
				if (t > 0) set.Add(t);
			return set;
		}

		private void IconHovered()
		{
			Main.LocalPlayer.mouseInterface = true;
			var click = BrowserSlotInteraction.Poll();

			if (_task.AcceptTypes.Length > 1)
			{
				string label = ItemLabel();
				var members = TagMembers();
				BrowserHover.SetTag(label, members);
				BrowserSlotInteraction.HandleTag(click, label, members);
			}
			else
			{
				BrowserHover.SetItem(IconType);
				BrowserSlotInteraction.HandleItem(click, IconType, inFavoritesPane: false);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();

			if (!_task.IsItem)
			{
				Terraria.Utils.DrawBorderString(sb, "- Manual checkmark",
					new Vector2(d.X + 6, d.Y + 5), new Color(190, 190, 205), 0.78f);
				return;
			}

			int iconType = IconType;
			if (iconType > 0)
			{
				Rectangle box = IconBox();
				bool hover = box.Contains(ModalEscape.PollCursor());
				var px = TextureAssets.MagicPixel.Value;
				if (hover)
					sb.Draw(px, box, new Color(120, 140, 220) * 0.4f);
				QuestbookIcon.Draw(sb, iconType, box.Center.ToVector2(), 20f);
				if (hover)
				{
					var bc = new Color(255, 224, 130);
					sb.Draw(px, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, 1), bc);
					sb.Draw(px, new Rectangle(box.X - 1, box.Bottom, box.Width + 2, 1), bc);
					sb.Draw(px, new Rectangle(box.X - 1, box.Y - 1, 1, box.Height + 2), bc);
					sb.Draw(px, new Rectangle(box.Right, box.Y - 1, 1, box.Height + 2), bc);
					Main.instance.MouseText(
						$"{ItemLabel()}\n[c/AAAAAA:LMB] how to obtain   [c/AAAAAA:RMB] used as");
					IconHovered();
				}
			}

			string name = ItemLabel();
			if (string.IsNullOrEmpty(name)) name = "(unresolved)";

			bool latched = QuestbookProgress.IsTaskSatisfied(_questId, _index);
			int have = 0;
			foreach (int t in _task.AcceptTypes)
				have += QuestbookSystem.CountInInventory(Main.LocalPlayer, t);
			bool resolved = _task.AcceptTypes.Length > 0;
			bool ok = latched || (resolved && have >= _task.Count);
			string progress = !resolved ? ""
				: latched ? "  (done)"
				: $"  ({have}/{_task.Count})";

			Terraria.Utils.DrawBorderString(sb, $"{_task.Count}x {name}{progress}",
				new Vector2(d.X + 30, d.Y + 5), ok ? new Color(120, 230, 120) : Color.White, 0.78f);

			if (_task.IsFluid && !ok)
			{
				Rectangle skip = SkipBox();
				bool sh = skip.Contains(ModalEscape.PollCursor());
				sb.Draw(TextureAssets.MagicPixel.Value, skip,
					sh ? new Color(80, 90, 150) : new Color(55, 60, 95));
				const string label = "I made it";
				Vector2 sz = FontAssets.MouseText.Value.MeasureString(label) * 0.7f;
				Terraria.Utils.DrawBorderString(sb, label,
					new Vector2(skip.Center.X - sz.X * 0.5f, skip.Center.Y - sz.Y * 0.5f),
					Color.White, 0.7f);
				if (sh)
					Main.LocalPlayer.mouseInterface = true;
			}
		}
	}

	private sealed class CompleteButton : UIElement
	{
		private readonly string _questId;

		public CompleteButton(string questId)
		{
			_questId = questId;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(30);
			OnLeftClick += (_, _) =>
			{
				if (!QuestbookProgress.IsComplete(_questId))
					QuestbookProgress.MarkManual(_questId);
			};
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			bool complete = QuestbookProgress.IsComplete(_questId);
			var box = new Rectangle((int)d.X + 4, (int)d.Y + 4, (int)d.Width - 8, 22);

			Color fill = complete
				? new Color(40, 90, 40)
				: (IsMouseHovering ? new Color(80, 90, 150) : new Color(55, 60, 95));
			sb.Draw(TextureAssets.MagicPixel.Value, box, fill);

			string label = complete ? "Completed" : "Mark Complete";
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.82f;
			Terraria.Utils.DrawBorderString(sb, label,
				new Vector2(box.Center.X - size.X * 0.5f, box.Center.Y - size.Y * 0.5f),
				complete ? new Color(140, 230, 140) : Color.White, 0.82f);
		}
	}

	private sealed class TaskEditRow : UIElement
	{
		private readonly TaskData _task;
		private readonly UITextButton _typeBtn;
		private readonly UITextButton _pickBtn;
		private readonly UITextField _countField;
		private readonly UITextButton _removeBtn;

		private const int IconX = 56;
		private const int IconSize = 20;

		public TaskEditRow(QuestbookUIState owner, QuestData quest, TaskData task)
		{
			_task = task;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(26);

			_typeBtn = new UITextButton(
				() => task.Type == "item" ? "item" : "check",
				() => QuestbookEditor.SetField(quest, _ =>
					task.Type = task.Type == "item" ? "checkmark" : "item"),
				tooltip: "Toggle task type");

			_pickBtn = new UITextButton(
				() => task.Items.Count > 0 ? task.Items[0] : "(pick item)",
				() => ItemPickerSystem.Open(type =>
				{
					string id = IngredientResolverImpl.StableItemId(type);
					QuestbookEditor.SetField(quest, _ =>
					{
						task.Items.Clear();
						if (id.Length > 0) task.Items.Add(id);
					});
					owner.RefreshDetail(quest);
				}),
				tooltip: "Search for an item")
			{ IsVisible = () => task.Type == "item" };

			_countField = new UITextField(
				() => task.Count.ToString(),
				v => QuestbookEditor.SetField(quest, _ =>
				{
					if (int.TryParse(v, out int n)) task.Count = System.Math.Max(1, n);
				}),
				maxLength: 5, filter: char.IsDigit, placeholder: "1");

			_removeBtn = new UITextButton(() => "X",
				() => { QuestbookEditor.RemoveTask(quest, task); owner.RefreshDetail(quest); },
				tooltip: "Remove task");

			Append(_typeBtn);
			Append(_pickBtn);
			Append(_countField);
			Append(_removeBtn);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float w = GetInnerDimensions().Width;
			if (w <= 0) return;

			const int h = 22, top = 2;
			Set(_typeBtn, 0, 52, top, h);
			Set(_removeBtn, (int)w - 22, 22, top, h);
			Set(_countField, (int)w - 22 - 4 - 34, 34, top, h);
			int pickX = IconX + IconSize + 4;
			int pickW = System.Math.Max(40, (int)w - 22 - 4 - 34 - 4 - pickX);
			Set(_pickBtn, pickX, pickW, top, h);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (_task.Type != "item") return;
			int type = _task.Items.Count > 0 ? IngredientResolverImpl.Instance.ResolveItemType(_task.Items[0]) : 0;
			if (type <= 0) return;
			CalculatedStyle d = GetDimensions();
			var box = new Rectangle((int)d.X + IconX, (int)d.Y + 3, IconSize, IconSize);
			QuestbookIcon.Draw(sb, type, box.Center.ToVector2(), IconSize);
		}

		private static void Set(UIElement e, int left, int width, int top, int height)
		{
			e.Left   = StyleDimension.FromPixels(left);
			e.Width  = StyleDimension.FromPixels(width);
			e.Top    = StyleDimension.FromPixels(top);
			e.Height = StyleDimension.FromPixels(height);
			e.Recalculate();
		}
	}

	private sealed class DepEditRow : UIElement
	{
		private readonly string _dep;
		private readonly UITextButton _removeBtn;

		public DepEditRow(QuestbookUIState owner, QuestData quest, string dep)
		{
			_dep = dep;
			Width = StyleDimension.Fill;
			Height = StyleDimension.FromPixels(22);

			_removeBtn = new UITextButton(() => "X",
				() => { QuestbookEditor.ToggleDep(dep, quest.Id); owner.RefreshDetail(quest); },
				tooltip: "Remove dependency");
			Append(_removeBtn);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float w = GetInnerDimensions().Width;
			if (w <= 0) return;
			_removeBtn.Left   = StyleDimension.FromPixels((int)w - 22);
			_removeBtn.Width  = StyleDimension.FromPixels(22);
			_removeBtn.Top    = StyleDimension.FromPixels(1);
			_removeBtn.Height = StyleDimension.FromPixels(20);
			_removeBtn.Recalculate();
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			string title = QuestbookSystem.QuestsById.TryGetValue(_dep, out QuestData? q)
				&& !string.IsNullOrEmpty(q.Title) ? q.Title : _dep;
			Terraria.Utils.DrawBorderString(sb, $"- {title}",
				new Vector2(d.X + 4, d.Y + 3), new Color(200, 205, 225), 0.7f);
		}
	}
}
