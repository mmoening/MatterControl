﻿/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetTemplatePrinter
	{
		private PrinterConfig printer;
		private double[] activeOffsets;
		private double nozzleWidth;
		private int firstLayerSpeed;

		public NozzleOffsetTemplatePrinter(PrinterConfig printer)
		{
			this.printer = printer;

			// Build offsets
			activeOffsets = new double[41];
			activeOffsets[20] = 0;

			var offsetStep = 1.2d / 20;

			for (var i = 1; i <= 20; i++)
			{
				activeOffsets[20 - i] = i * offsetStep * -1;
				activeOffsets[20 + i] = i * offsetStep;
			}

			nozzleWidth = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
			firstLayerSpeed = (int)(printer.Settings.GetValue<double>(SettingsKey.first_layer_speed) * 60);
		}

		public double[] ActiveOffsets => activeOffsets;

		public bool DebugMode { get; private set; } = false;

		public void BuildTemplate(GCodeSketch gcodeSketch, GCodeSketch sketch2, bool verticalLayout)
		{
			if (verticalLayout)
			{
				gcodeSketch.Transform = Affine.NewRotation(MathHelper.DegreesToRadians(90)) * Affine.NewTranslation(120, 45);
				sketch2.Transform = gcodeSketch.Transform;
			}
			else
			{
				gcodeSketch.Transform = Affine.NewTranslation(90, 175);
				sketch2.Transform = gcodeSketch.Transform;
			}

			var rect = new RectangleDouble(0, 0, 123, 30);

			var originalRect = rect;

			int towerSize = 10;

			gcodeSketch.Speed = firstLayerSpeed;

			double y1 = rect.Bottom;
			gcodeSketch.MoveTo(rect.Left, y1);

			gcodeSketch.PenDown();

			var towerRect = new RectangleDouble(0, 0, towerSize, towerSize);
			towerRect.Offset(originalRect.Left - towerSize, originalRect.Bottom);

			// Prime
			if (verticalLayout)
			{
				this.PrimeHotend(gcodeSketch, towerRect);
			}

			// Perimeters
			rect = this.CreatePerimeters(gcodeSketch, rect);

			double x, y2, y3, y4;
			double sectionHeight = rect.Height / 2;
			bool up = true;
			var step = (rect.Width - 3) / 40;

			if (!this.DebugMode)
			{
				y1 = rect.YCenter + (nozzleWidth / 2);

				// Draw centerline
				gcodeSketch.MoveTo(rect.Left, y1);
				gcodeSketch.LineTo(rect.Right, y1);
				y1 += nozzleWidth;
				gcodeSketch.MoveTo(rect.Right, y1);
				gcodeSketch.LineTo(rect.Left, y1);

				y1 -= nozzleWidth / 2;

				x = rect.Left + 1.5;

				y2 = y1 - sectionHeight - (nozzleWidth * 1.5);
				y3 = y2 - 2;
				y4 = y2 - 5;

				bool drawGlyphs = true;

				var inverseTransform = gcodeSketch.Transform;
				inverseTransform.invert();

				// Draw calibration lines
				for (var i = 0; i <= 40; i++)
				{
					gcodeSketch.MoveTo(x, up ? y1 : y2);

					if ((i % 5 == 0))
					{
						gcodeSketch.LineTo(x, y4);

						if (i < 20)
						{
							gcodeSketch.MoveTo(x, y3);
						}

						var currentPos = gcodeSketch.CurrentPosition;
						currentPos = inverseTransform.Transform(currentPos);

						PrintLineEnd(gcodeSketch, drawGlyphs, i, currentPos);
					}

					gcodeSketch.LineTo(x, up ? y2 : y1);

					x = x + step;

					up = !up;
				}
			}

			gcodeSketch.PenUp();

			x = rect.Left + 1.5;
			y1 = rect.Top + (nozzleWidth * .5);
			y2 = y1 - sectionHeight + (nozzleWidth * .5);

			sketch2.PenUp();
			sketch2.MoveTo(rect.Left, rect.Top);
			sketch2.PenDown();

			towerRect = new RectangleDouble(0, 0, towerSize, towerSize);
			towerRect.Offset(originalRect.Left - towerSize, originalRect.Top - towerSize);

			// Prime
			if (verticalLayout)
			{
				this.PrimeHotend(sketch2, towerRect);
			}

			if (this.DebugMode)
			{
				// Perimeters
				rect = this.CreatePerimeters(gcodeSketch, rect);
			}
			else
			{
				up = true;

				// Draw calibration lines
				for (var i = 0; i <= 40; i++)
				{
					sketch2.MoveTo(x + activeOffsets[i], up ? y1 : y2, retract: false);
					sketch2.LineTo(x + activeOffsets[i], up ? y2 : y1);

					x = x + step;

					up = !up;
				}
			}

			sketch2.PenUp();
		}

		private RectangleDouble CreatePerimeters(GCodeSketch gcodeSketch, RectangleDouble rect)
		{
			gcodeSketch.WriteRaw("; CreatePerimeters");
			for (var i = 0; i < 2; i++)
			{
				rect.Inflate(-nozzleWidth);
				gcodeSketch.DrawRectangle(rect);
			}

			return rect;
		}

		private void PrimeHotend(GCodeSketch gcodeSketch, RectangleDouble towerRect)
		{
			gcodeSketch.WriteRaw("; Priming");

			while (towerRect.Width > 4)
			{
				towerRect.Inflate(-nozzleWidth);
				gcodeSketch.DrawRectangle(towerRect);
			}
		}

		private static void PrintLineEnd(GCodeSketch turtle, bool drawGlyphs, int i, Vector2 currentPos, bool lift = false)
		{
			var originalSpeed = turtle.Speed;
			turtle.Speed = Math.Min(700, turtle.Speed);

			if (drawGlyphs && CalibrationLine.Glyphs.TryGetValue(i, out IVertexSource vertexSource))
			{
				turtle.WriteRaw("; LineEnd Marker");
				var flattened = new FlattenCurves(vertexSource);

				var verticies = flattened.Vertices();
				var firstItem = verticies.First();
				var position = turtle.CurrentPosition;

				var scale = 0.3;

				if (firstItem.command != ShapePath.FlagsAndCommand.MoveTo)
				{
					if (lift)
					{
						turtle.PenUp();
					}

					turtle.MoveTo((firstItem.position * scale) + currentPos);
				}

				bool closed = false;

				foreach (var item in verticies)
				{
					switch (item.command)
					{
						case ShapePath.FlagsAndCommand.MoveTo:
							if (lift)
							{
								turtle.PenUp();
							}

							turtle.MoveTo((item.position * scale) + currentPos);

							if (lift)
							{
								turtle.PenDown();
							}
							break;

						case ShapePath.FlagsAndCommand.LineTo:
							turtle.LineTo((item.position * scale) + currentPos);
							break;

						case ShapePath.FlagsAndCommand.FlagClose:
							turtle.LineTo((firstItem.position * scale) + currentPos);
							closed = true;
							break;
					}
				}

				bool atStartingPosition = position.Equals(turtle.CurrentPosition, .1);

				if (!closed
					&& !atStartingPosition)
				{
					turtle.LineTo((firstItem.position * scale) + currentPos);
					atStartingPosition = position.Equals(turtle.CurrentPosition, .1);
				}

				// Restore original speed
				turtle.Speed = originalSpeed;

				if (!atStartingPosition)
				{
					// Return to original position
					turtle.PenUp();
					turtle.MoveTo(currentPos);
					turtle.PenDown();
				}
			}
		}
	}
}
