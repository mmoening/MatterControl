﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class TextImageWidget : GuiWidget
	{
		private ImageBuffer image;
		protected RGBA_Bytes fillColor = new RGBA_Bytes(0, 0, 0, 0);
		protected RGBA_Bytes borderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
		protected double borderWidth = 1;
		protected double borderRadius = 0;

		public TextImageWidget(string label, RGBA_Bytes fillColor, RGBA_Bytes borderColor, RGBA_Bytes textColor, double borderWidth, BorderDouble margin, ImageBuffer image = null, double fontSize = 12, FlowDirection flowDirection = FlowDirection.LeftToRight, double height = 40, double width = 0, bool centerText = false, double imageSpacing = 0)
			: base()
		{
			this.image = image;
			this.fillColor = fillColor;
			this.borderColor = borderColor;
			this.borderWidth = borderWidth;
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(0);

			TextWidget textWidget = new TextWidget(label, pointSize: fontSize);
			ImageWidget imageWidget;

			FlowLayoutWidget container = new FlowLayoutWidget(flowDirection);

			if (centerText)
			{
				// make sure the contents are centered
				GuiWidget leftSpace = new GuiWidget(0, 1);
				leftSpace.HAnchor = HAnchor.Stretch;
				container.AddChild(leftSpace);
			}

			if (image != null && image.Width > 0)
			{
				imageWidget = new ImageWidget(image);
				imageWidget.VAnchor = VAnchor.Center;
				imageWidget.Margin = new BorderDouble(right: imageSpacing);
				container.AddChild(imageWidget);
			}

			if (label != "")
			{
				textWidget.VAnchor = VAnchor.Center;
				textWidget.TextColor = textColor;
				textWidget.Padding = new BorderDouble(3, 0);
				container.AddChild(textWidget);
			}

			if (centerText)
			{
				GuiWidget rightSpace = new GuiWidget(0, 1);
				rightSpace.HAnchor = HAnchor.Stretch;
				container.AddChild(rightSpace);

				container.HAnchor = HAnchor.Stretch | HAnchor.Fit;
			}
			container.VAnchor = VAnchor.Center;

			container.MinimumSize = new Vector2(width, height);
			container.Margin = margin;
			this.AddChild(container);
			HAnchor = HAnchor.Stretch | HAnchor.Fit;
			VAnchor = VAnchor.Center | VAnchor.Fit;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (borderColor.Alpha0To255 > 0)
			{
				RectangleDouble borderRectangle = LocalBounds;

				if (borderWidth > 0)
				{
					if (borderWidth == 1)
					{
						graphics2D.Rectangle(borderRectangle, borderColor);
					}
					else
					{
						//boarderRectangle.Inflate(-borderWidth / 2);
						RoundedRect rectBorder = new RoundedRect(borderRectangle, this.borderRadius);

						graphics2D.Render(new Stroke(rectBorder, borderWidth), borderColor);
					}
				}
			}

			if (this.fillColor.Alpha0To255 > 0)
			{
				RectangleDouble insideBounds = LocalBounds;
				insideBounds.Inflate(-this.borderWidth);
				RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(this.borderRadius - this.borderWidth, 0));

				graphics2D.Render(rectInside, this.fillColor);
			}

			base.OnDraw(graphics2D);
		}
	}
}