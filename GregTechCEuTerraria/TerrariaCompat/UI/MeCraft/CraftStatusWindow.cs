#nullable enable
using System;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using GregTechCEuTerraria.TerrariaCompat.UI.PatternAccess;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Chat;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;

public sealed class CraftStatusWindow : UIModalWindow
{
	private Point16 _termPos;
	private UIText? _title;

	public void Bind(Point16 termPos)
	{
		_termPos = termPos;
		RemoveAllChildren();
		BuildPanel();
	}

	public void Unbind() => RemoveAllChildren();

	private void BuildPanel()
	{
		const int W = 760, H = 600;
		const int margin = 12, cpuW = 200, gap = 8, contentTop = 52;
		int contentH = H - contentTop - 52;
		int tableW = W - margin * 2 - cpuW - gap;
		int btnTop = H - 44;

		var panel = new UITerrariaPanel
		{
			Width = StyleDimension.FromPixels(W),
			Height = StyleDimension.FromPixels(H),
			HAlign = 0.5f,
			VAlign = 0.4f,
		};

		_title = new UIText("Crafting Status", 0.95f)
		{ Left = StyleDimension.FromPixels(margin), Top = StyleDimension.FromPixels(10) };
		panel.Append(_title);

		panel.Append(new UIText("Click a slot to see where it's crafted", 0.72f)
		{
			Left = StyleDimension.FromPixels(margin),
			Top = StyleDimension.FromPixels(32),
			TextColor = new Color(150, 165, 205),
		});

		panel.Append(new StatusTable(_termPos)
		{
			Left = StyleDimension.FromPixels(margin),
			Top = StyleDimension.FromPixels(contentTop),
			Width = StyleDimension.FromPixels(tableW),
			Height = StyleDimension.FromPixels(contentH),
		});

		panel.Append(new CpuList(_termPos)
		{
			Left = StyleDimension.FromPixels(margin + tableW + gap),
			Top = StyleDimension.FromPixels(contentTop),
			Width = StyleDimension.FromPixels(cpuW),
			Height = StyleDimension.FromPixels(contentH),
		});

		panel.Append(new UITextButton(
			label: () => "Cancel Job",
			onLeft: CancelSelected,
			tooltip: "Cancel the selected CPU's crafting job",
			width: 160, height: 36, textScale: 0.95f)
		{ Left = StyleDimension.FromPixels(margin), Top = StyleDimension.FromPixels(btnTop) });

		panel.Append(new UITextButton(
			label: () => "Close",
			onLeft: MeCraftStatusSystem.Close,
			tooltip: null, width: 120, height: 36, textScale: 0.95f)
		{ Left = StyleDimension.FromPixels(W - margin - 120), Top = StyleDimension.FromPixels(btnTop) });

		Append(panel);
	}

	private void CancelSelected()
	{
		var s = MeCraftStatusSystem.LastSnapshot;
		int i = MeCraftStatusSystem.SelectedIndex;
		if (i >= 0 && i < s.Cpus.Count) MeCraftPackets.Cancel(s.Cpus[i].Pos);
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (Main.GameUpdateCount % 30 == 0)
			MeCraftPackets.RequestStatus(_termPos, MeCraftStatusSystem.SelectedIndex);
		if (_title != null) _title.SetText(TitleText());
	}

	private static string TitleText()
	{
		var s = MeCraftStatusSystem.LastSnapshot;
		string t = "Crafting Status";
		double remaining = s.RemainingItemCount, start = s.StartItemCount;
		long eta = (long)(s.ElapsedNs / Math.Max(1d, start - remaining) * remaining);
		if (eta > 0 && s.Entries.Count > 0)
			t += " - " + Dur(eta / 1_000_000);
		if (s.CantStore) t += "  [c/FF5555:(full)]";
		return t;
	}

	internal static string Fmt(long n)
		=> n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.#") + "M"
		 : n >= 10_000 ? (n / 1_000.0).ToString("0.#") + "k"
		 : n.ToString();

	private static string Dur(long ms)
	{
		var ts = TimeSpan.FromMilliseconds(ms);
		return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
								  : $"{ts.Minutes}:{ts.Seconds:00}";
	}

	internal sealed class CpuList : UIElement
	{
		private readonly Point16 _termPos;
		private int _scroll;
		private const int RowH = 38, Gap = 2;
		private static readonly Item[] _slot = { new() };

		public CpuList(Point16 termPos) => _termPos = termPos;

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (!IsMouseHovering) return;
			Scrollbar.Wheel(evt, ref _scroll);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var b = GetDimensions().ToRectangle();
			var px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, b, new Color(20, 22, 50) * 0.5f);
			if (IsMouseHovering)
			{
				Main.LocalPlayer.mouseInterface = true;
				PlayerInput.LockVanillaMouseScroll("GregTechCEuTerraria/MeCraftCpuList");
			}

			Terraria.Utils.DrawBorderString(sb, "Crafting CPUs",
				new Vector2(b.X + 6, b.Y + 3), new Color(220, 230, 255), 0.7f);

			var cpus = MeCraftStatusSystem.LastSnapshot.Cpus;
			int listTop = b.Y + 18;
			int rowStep = RowH + Gap;
			int viewRows = Math.Max(1, (b.Bottom - listTop) / rowStep);
			int maxRowScroll = Math.Max(0, cpus.Count - viewRows);
			int rowScroll = Math.Min(_scroll / rowStep, maxRowScroll);
			int selected = cpus.Count > 0 ? Math.Clamp(MeCraftStatusSystem.SelectedIndex, 0, cpus.Count - 1) : -1;

			var mouse = ModalEscape.PollCursor();
			int hoveredIdx = -1;

			float oldScale = Main.inventoryScale;
			Main.inventoryScale = 16f / 52f;
			try
			{
				for (int r = 0; r < viewRows; r++)
				{
					int i = rowScroll + r;
					if (i >= cpus.Count) break;
					var c = cpus[i];
					var row = new Rectangle(b.X + 4, listTop + r * rowStep, b.Width - 8, RowH);
					bool sel = i == selected;
					bool hov = row.Contains(mouse);

					Color bg = sel ? new Color(70, 90, 50) * 0.95f
						: (hov ? new Color(60, 64, 110) : new Color(40, 44, 86)) * 0.9f;
					sb.Draw(px, row, bg);
					if (sel)
					{
						sb.Draw(px, new Rectangle(row.X, row.Y, row.Width, 1), new Color(230, 220, 80));
						sb.Draw(px, new Rectangle(row.X, row.Bottom - 1, row.Width, 1), new Color(230, 220, 80));
					}

					Terraria.Utils.DrawBorderString(sb, $"CPU {i + 1}",
						new Vector2(row.X + 4, row.Y + 3), Color.White, 0.7f);

					if (c.Busy && c.Output != null)
					{
						var iconRect = new Rectangle(row.Right - 20, row.Y + 18, 16, 16);
						if (c.Output is AEItemKey ik)
						{
							var s = ik.GetReadOnlyStack().Clone(); s.stack = 1;
							_slot[0] = s;
							ItemSlot.Draw(sb, _slot, ItemSlot.Context.CraftingMaterial, 0, new Vector2(iconRect.X, iconRect.Y));
						}
						else if (c.Output is AEFluidKey fk)
						{
							BrowserFluidSlot.Draw(sb, iconRect, fk.GetFluid(), amountMb: 0);
						}
						Terraria.Utils.DrawBorderString(sb, $"{Fmt(c.OutputAmount)}x",
							new Vector2(row.X + 4, row.Y + 19), new Color(124, 255, 124), 0.62f);
						int pw = (int)((row.Width - 2) * Math.Clamp(c.Progress, 0f, 1f));
						sb.Draw(px, new Rectangle(row.X + 1, row.Bottom - 3, pw, 2), new Color(124, 255, 124));
					}
					else if (c.CantStore)
					{
						Terraria.Utils.DrawBorderString(sb, "Can't store results",
							new Vector2(row.X + 4, row.Y + 19), new Color(255, 110, 110), 0.62f);
					}
					else
					{
						Terraria.Utils.DrawBorderString(sb, "Idle",
							new Vector2(row.X + 4, row.Y + 19), new Color(160, 160, 175), 0.62f);
					}

					if (hov) hoveredIdx = i;
				}
			}
			finally { Main.inventoryScale = oldScale; }

			if (hoveredIdx >= 0)
			{
				if (MouseClick.LeftPressed)
				{
					MeCraftStatusSystem.SelectedIndex = hoveredIdx;
					MeCraftPackets.RequestStatus(_termPos, hoveredIdx);
				}
				EmitCpuTooltip(cpus[hoveredIdx], hoveredIdx);
			}
		}

		private static void EmitCpuTooltip(CraftCpuStatus c, int idx)
		{
			var body = new System.Text.StringBuilder($"CPU {idx + 1}");
			body.Append($"\n[c/C8C8C8:Storage: {c.Bytes:N0} bytes]");
			body.Append($"\n[c/C8C8C8:Co-processors: {c.CoProcessors}]");
			if (c.Busy && c.Output != null)
			{
				body.Append($"\n[c/7CFF7C:Crafting {c.OutputAmount:N0}x {c.Output.GetDisplayName()}]");
				body.Append($"\n[c/88AAFF:{(int)(c.Progress * 100)}% done]");
			}
			else body.Append("\n[c/AAAAAA:Idle]");
			Main.instance.MouseText(body.ToString());
		}
	}

	internal sealed class StatusTable : UIElement
	{
		private readonly Point16 _termPos;
		private int _scroll;
		private AEKey? _pendingLocate;

		public StatusTable(Point16 termPos) => _termPos = termPos;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_pendingLocate is { } what)
			{
				_pendingLocate = null;
				LocateCrafter(what);
			}
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (!IsMouseHovering) return;
			Scrollbar.Wheel(evt, ref _scroll);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var area = GetDimensions().ToRectangle();
			var px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, area, new Color(20, 22, 50) * 0.5f);
			if (IsMouseHovering)
			{
				Main.LocalPlayer.mouseInterface = true;
				PlayerInput.LockVanillaMouseScroll("GregTechCEuTerraria/MeCraftStatus");
			}

			var snapshot = MeCraftStatusSystem.LastSnapshot;
			var entries = snapshot.Entries;

			int gridTop = area.Y;
			if (!string.IsNullOrEmpty(snapshot.StallReason))
			{
				Terraria.Utils.DrawBorderString(sb, "Stalled - " + snapshot.StallReason,
					new Vector2(area.X + 6, area.Y + 4), new Color(255, 110, 110), 0.62f);
				gridTop = area.Y + 20;
			}

			if (entries.Count == 0)
			{
				if (string.IsNullOrEmpty(snapshot.StallReason))
					Terraria.Utils.DrawBorderString(sb, "Nothing crafting",
						new Vector2(area.X + 8, area.Y + 8), Color.LightGray, 0.85f);
				return;
			}

			int cols = Math.Max(1, (area.Width + CraftCell.Pad) / CraftCell.Step);
			int rows = (entries.Count + cols - 1) / cols;
			int viewRows = Math.Max(1, (area.Bottom - gridTop) / CraftCell.Step);
			int maxRowScroll = Math.Max(0, rows - viewRows);
			int rowScroll = Math.Min(_scroll / CraftCell.Step, maxRowScroll);

			var mouse = ModalEscape.PollCursor();
			CraftStatusEntry? hovered = null;

			for (int r = 0; r < viewRows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
					int i = (r + rowScroll) * cols + c;
					if (i >= entries.Count) break;
					var e = entries[i];
					var rect = new Rectangle(area.X + c * CraftCell.Step, gridTop + r * CraftCell.Step,
						CraftCell.Size, CraftCell.Size);

					Color bg = e.Active > 0 ? new Color(30, 95, 45) * 0.7f
						: e.Pending > 0 ? new Color(110, 90, 22) * 0.7f
						: new Color(30, 34, 64) * 0.7f;
					bool hov = rect.Contains(mouse);
					CraftCell.Draw(sb, rect, e.What, 0, bg, hov, info: hov ? Breakdown(e) : null);
					DrawBreakdown(sb, rect, e);
					if (hov) hovered = e;
				}
			}

			if (hovered != null && MouseClick.LeftPressed)
				_pendingLocate = hovered.Value.What;
		}

		private void LocateCrafter(AEKey what)
		{
			var machine = WorldCapability.Get<MetaMachine>(_termPos.X, _termPos.Y);
			var net = machine != null ? MeNetworkSystem.NetOf(machine) : null;
			if (net is null) return;
			foreach (var p in net.Providers)
				foreach (var pat in p.Patterns)
					if (what.Equals(pat.PrimaryOutput))
					{
						PatternLocatorSystem.Locate(p.ProviderPos);
						MachineUISystem.Close();
						return;
					}
		}

		private static string Breakdown(CraftStatusEntry e)
		{
			var b = new System.Text.StringBuilder();
			if (e.Active > 0)  b.Append($"Crafting: {e.Active:N0}\n");
			if (e.Pending > 0) b.Append($"Scheduled: {e.Pending:N0}\n");
			if (e.Stored > 0)  b.Append($"Stored: {e.Stored:N0}\n");
			return b.ToString().TrimEnd('\n');
		}

		private static void DrawBreakdown(SpriteBatch sb, Rectangle rect, CraftStatusEntry e)
		{
			var font = FontAssets.ItemStack.Value;
			const float s = 0.6f;
			if (e.Pending > 0)
				ChatManager.DrawColorCodedStringWithShadow(sb, font, Fmt(e.Pending),
					new Vector2(rect.X + 2, rect.Y + 1), new Color(255, 220, 80), 0f, Vector2.Zero, new Vector2(s));
			if (e.Active > 0)
				ChatManager.DrawColorCodedStringWithShadow(sb, font, Fmt(e.Active),
					new Vector2(rect.X + 2, rect.Bottom - 13), new Color(124, 255, 124), 0f, Vector2.Zero, new Vector2(s));
			if (e.Stored > 0)
			{
				string t = Fmt(e.Stored);
				var size = ChatManager.GetStringSize(font, t, new Vector2(s));
				ChatManager.DrawColorCodedStringWithShadow(sb, font, t,
					new Vector2(rect.Right - size.X - 3, rect.Bottom - 13), Color.White, 0f, Vector2.Zero, new Vector2(s));
			}
		}
	}
}
