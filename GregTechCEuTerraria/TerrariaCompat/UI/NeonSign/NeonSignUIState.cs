#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Tiles.NeonSign;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.NeonSign;

public sealed class NeonSignUIState : UIModalWindow
{
	private const float Scale = 2f;
	private const int Pad = 8, W = 200;

	private NeonSignEntity? _sign;
	private string _text = "";
	private int _color;
	private int _size;

	public NeonSignEntity? Sign => _sign;

	public void Bind(NeonSignEntity sign)
	{
		_sign = sign;
		_text = sign.Text;
		_color = sign.ColorIndex;
		_size = sign.SizeStep;
		Build();
	}

	public void Unbind()
	{
		Flush();
		_sign = null;
		RemoveAllChildren();
	}

	private void Push()
	{
		if (_sign is null) return;
		NeonSignEditPacket.SendRequest(_sign.Position.X, _sign.Position.Y,
			_text, (byte)_color, (sbyte)System.Math.Clamp(_size, NeonSignEntity.MinStep, NeonSignEntity.MaxStep));
	}

	private void Flush()
	{
		UITextField.UnfocusAll();
		Push();
	}

	private void Build()
	{
		RemoveAllChildren();
		UITextField.UnfocusAll();

		var panel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(W * Scale),
			HAlign = 0.5f,
			VAlign = 0.4f,
		};

		panel.Append(new UIText("Neon Sign", 0.62f)
		{
			Left = StyleDimension.FromPixels(Pad * Scale),
			Top  = StyleDimension.FromPixels(Pad * Scale),
		});

		int inner = W - Pad * 2;

		int taY = 22;
		int taH = 52;
		panel.Append(new UITextArea(() => _text, t => { _text = t; Push(); },
			maxLength: NeonSignEntity.MaxTextLength, placeholder: "type sign text", commitOnEscape: true)
		{
			Left   = StyleDimension.FromPixels(Pad * Scale),
			Top    = StyleDimension.FromPixels(taY * Scale),
			Width  = StyleDimension.FromPixels(inner * Scale),
			Height = StyleDimension.FromPixels(taH * Scale),
		});

		const int Cols = 4, SwatchGap = 2, SwatchH = 14;
		int swatchW = (inner - SwatchGap * (Cols - 1)) / Cols;
		int colorY = taY + taH + 6;
		for (int i = 0; i < NeonSignPalette.Count; i++)
		{
			int idx = i;
			int cx = Pad + (i % Cols) * (swatchW + SwatchGap);
			int cy = colorY + (i / Cols) * (SwatchH + SwatchGap);
			panel.Append(new NeonSignColorButton(idx, () => _color, SetColor)
			{
				Left   = StyleDimension.FromPixels(cx * Scale),
				Top    = StyleDimension.FromPixels(cy * Scale),
				Width  = StyleDimension.FromPixels(swatchW * Scale),
				Height = StyleDimension.FromPixels(SwatchH * Scale),
			});
		}

		int rows = (NeonSignPalette.Count + Cols - 1) / Cols;
		int sizeY = colorY + rows * (SwatchH + SwatchGap) + 6;

		const int SmallW = 42, BtnH = 16;
		panel.Append(new UITextButton(() => "Size -", onLeft: () => SetSize(-1), onRight: () => SetSize(-1),
			width: (int)(SmallW * Scale), height: (int)(BtnH * Scale))
		{
			Left = StyleDimension.FromPixels(Pad * Scale),
			Top  = StyleDimension.FromPixels(sizeY * Scale),
		});
		panel.Append(new UIDynamicLabel(() => $"Size {_size}", 0.6f, new Color(220, 230, 255))
		{
			Left = StyleDimension.FromPixels((Pad + SmallW + 8) * Scale),
			Top  = StyleDimension.FromPixels((sizeY + 4) * Scale),
		});
		panel.Append(new UITextButton(() => "Size +", onLeft: () => SetSize(1), onRight: () => SetSize(1),
			width: (int)(SmallW * Scale), height: (int)(BtnH * Scale))
		{
			Left = StyleDimension.FromPixels((Pad + inner - SmallW) * Scale),
			Top  = StyleDimension.FromPixels(sizeY * Scale),
		});

		panel.Height = StyleDimension.FromPixels((sizeY + BtnH + Pad) * Scale);
		Append(panel);
	}

	private void SetColor(int idx)
	{
		_color = idx;
		Push();
	}

	private void SetSize(int delta)
	{
		_size = System.Math.Clamp(_size + delta, NeonSignEntity.MinStep, NeonSignEntity.MaxStep);
		Push();
	}
}
