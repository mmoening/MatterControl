﻿/*
Copyright (c) 2014, Lars Brubaker
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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MoveInZControl : InteractionVolume
	{
		public IObject3D ActiveSelectedItem;
		protected PlaneShape hitPlane;
		protected Vector3 initialHitPosition;
		protected Mesh upArrowMesh;
		protected AxisAlignedBoundingBox mouseDownSelectedBounds;
		protected Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private double distToStart = 5;
		private double lineLength = 55;
		private List<Vector2> lines = new List<Vector2>();
		private double upArrowSize = 7 * GuiWidget.DeviceScale;
		private InlineEditControl zHeightDisplayInfo;
		private bool HadClickOnControl;

		public MoveInZControl(IInteractionVolumeContext context)
			: base(context)
		{
			Name = "MoveInZControl";
			zHeightDisplayInfo = new InlineEditControl()
			{
				ForceHide = () =>
				{
					var selectedItem = InteractionContext.Scene.RootSelectedItem;
					// if the selection changes
					if (selectedItem != ActiveSelectedItem)
					{
						return true;
					}

					// if another control gets a hover
					if (InteractionContext.HoveredInteractionVolume != this
					&& InteractionContext.HoveredInteractionVolume != null)
					{
						return true;
					}

					// if we clicked on the control
					if (HadClickOnControl)
					{
						return false;
					}

					return false;
				}
,
				GetDisplayString = (value) => "{0:0.0#}mm".FormatWith(value)
			};

			zHeightDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!zHeightDisplayInfo.Visible)
				{
					HadClickOnControl = false;
				}
			};

			zHeightDisplayInfo.EditComplete += (s, e) =>
			{
				var selectedItem = InteractionContext.Scene.RootSelectedItem;

				Matrix4X4 startingTransform = selectedItem.Matrix;

				var newZPosition = zHeightDisplayInfo.Value;

				if (InteractionContext.SnapGridDistance > 0)
				{
					// snap this position to the grid
					double snapGridDistance = InteractionContext.SnapGridDistance;

					// snap this position to the grid
					newZPosition = ((int)((newZPosition / snapGridDistance) + .5)) * snapGridDistance;
				}

				AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				var moveAmount = newZPosition - originalSelectedBounds.minXYZ.Z;

				if (moveAmount != 0)
				{
					selectedItem.Matrix = selectedItem.Matrix * Matrix4X4.CreateTranslation(0, 0, moveAmount);
					Invalidate();
				}

				context.AddTransformSnapshot(startingTransform);
			};

			InteractionContext.GuiSurface.AddChild(zHeightDisplayInfo);

			DrawOnTop = true;

			string arrowFile = Path.Combine("Icons", "3D Icons", "up_pointer.stl");
			using (Stream arrowStream = AggContext.StaticData.OpenStream(arrowFile))
			{
				upArrowMesh = MeshFileIo.Load(arrowStream, Path.GetExtension(arrowFile), CancellationToken.None).Mesh;
			}

			CollisionVolume = upArrowMesh.CreateTraceData();

			InteractionContext.GuiSurface.AfterDraw += InteractionLayer_AfterDraw;
		}

		public override void DrawGlContent(DrawGlContentEventArgs e)
		{
			bool shouldDrawMoveControls = true;
			if (InteractionContext.SelectedInteractionVolume != null
				&& InteractionContext.SelectedInteractionVolume as MoveInZControl == null)
			{
				shouldDrawMoveControls = false;
			}

			var selectedItem = InteractionContext.Scene.RootSelectedItem;
			if (selectedItem != null)
			{
				if (shouldDrawMoveControls)
				{
					// don't draw if any other control is dragging
					if (MouseOver)
					{
						GLHelper.Render(upArrowMesh, Color.Red, TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(upArrowMesh, Color.Black, TotalTransform, RenderTypes.Shaded);
					}
				}
			}

			base.DrawGlContent(e);
		}

		public Vector3 GetTopPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			if (originalSelectedBounds.minXYZ.X != double.PositiveInfinity)
			{
				return new Vector3(originalSelectedBounds.Center.X, originalSelectedBounds.Center.Y, originalSelectedBounds.maxXYZ.Z);
			}

			return Vector3.Zero;
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			var selectedItem = InteractionContext.Scene.RootSelectedItem;

			if (mouseEvent3D.info != null 
				&& selectedItem != null)
			{
				HadClickOnControl = true;
				ActiveSelectedItem = selectedItem;

				zHeightDisplayInfo.Visible = true;

				double distanceToHit = Vector3.Dot(mouseEvent3D.info.HitPosition, mouseEvent3D.MouseRay.directionNormal);
				hitPlane = new PlaneShape(mouseEvent3D.MouseRay.directionNormal, distanceToHit, null);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = selectedItem.Matrix;
				mouseDownSelectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
			var selectedItem = InteractionContext.Scene.RootSelectedItem;
			ActiveSelectedItem = selectedItem;
			if (MouseOver)
			{
				zHeightDisplayInfo.Visible = true;
			}
			else if (!HadClickOnControl)
			{
				zHeightDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl)
			{
				IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

				if (info != null 
					&& selectedItem != null)
				{
					var delta = info.HitPosition.Z - initialHitPosition.Z;

					double newZPosition = mouseDownSelectedBounds.minXYZ.Z + delta;

					if (InteractionContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = InteractionContext.SnapGridDistance;

						// snap this position to the grid
						newZPosition = ((int)((newZPosition / snapGridDistance) + .5)) * snapGridDistance;
					}

					AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
					var moveAmount = newZPosition - originalSelectedBounds.minXYZ.Z;

					if (moveAmount != 0)
					{
						selectedItem.Matrix = selectedItem.Matrix * Matrix4X4.CreateTranslation(0, 0, moveAmount);
						Invalidate();
					}
				}
			}

			base.OnMouseMove(mouseEvent3D);
		}

		public override void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			InteractionContext.AddTransformSnapshot(transformOnMouseDown);
			base.OnMouseUp(mouseEvent3D);
		}

		public override void CancelOpperation()
		{
			IObject3D selectedItem = InteractionContext.Scene.RootSelectedItem;
			if (selectedItem != null
				&& MouseDownOnControl)
			{
				selectedItem.Matrix = transformOnMouseDown;
				MouseDownOnControl = false;
				MouseOver = false;

				InteractionContext.Scene.DrawSelection = true;
				InteractionContext.Scene.ShowSelectionShadow = true;
			}

			base.CancelOpperation();
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

			Vector3 topPosition = GetTopPosition(selectedItem);
			Vector3 bottomPosition = new Vector3(topPosition.X, topPosition.Y, selectedBounds.minXYZ.Z);
			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);

			Vector3 boxCenter = topPosition;
			boxCenter.Z += (10 + upArrowSize / 2) * distBetweenPixelsWorldSpace;

			Matrix4X4 centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;

			lines.Clear();
			// left lines
			// the lines on the bed
			var bedPosition = new Vector3(topPosition.X, topPosition.Y, 0);
			lines.Add(InteractionContext.World.GetScreenPosition(bedPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[0].X + lineLength, lines[0].Y));

			lines.Add(InteractionContext.World.GetScreenPosition(bottomPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[2].X + lineLength, lines[2].Y));
		}

		private void InteractionLayer_AfterDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = InteractionContext.Scene.RootSelectedItem;

			if (selectedItem != null
				&& lines.Count > 2)
			{
				if (zHeightDisplayInfo.Visible)
				{
					for (int i = 0; i < lines.Count; i += 2)
					{
						// draw the measure line
						drawEvent.Graphics2D.Line(lines[i], lines[i + 1], Color.Black);
					}

					for (int i = 0; i < lines.Count; i += 4)
					{
						DrawMeasureLine(drawEvent.Graphics2D, (lines[i] + lines[i + 1]) / 2, (lines[i + 2] + lines[i + 3]) / 2, Color.Black, LineArrows.Both);
					}

					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					zHeightDisplayInfo.Value = selectedBounds.minXYZ.Z;
					zHeightDisplayInfo.OriginRelativeParent = lines[1] + new Vector2(10, - zHeightDisplayInfo.LocalBounds.Center.Y);
				}
			}
		}
	}
}
