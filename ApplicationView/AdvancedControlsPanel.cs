﻿/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using MatterHackers.MatterControl.PrintLibrary;

namespace MatterHackers.MatterControl
{
	public class AdvancedControlsPanel : GuiWidget
	{
		private EventHandler unregisterEvents;

		private TabControl tabControl;

		public AdvancedControlsPanel()
		{
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			tabControl = CreateTabControl();
			AddChild(tabControl);
			AnchorAll();

			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => UiThread.RunOnIdle(ReloadSliceSettings), ref unregisterEvents);
		}

		public static string SliceSettingsTabName { get; } = "Slice Settings Tab";

		public void ReloadSliceSettings()
		{
			if (tabControl.HasBeenClosed)
			{
				return;
			}

			PopOutManager.SaveIfClosed = false;
			// remove the advance control and replace it with new ones built for the selected printer
			int advancedControlsIndex = GetChildIndex(tabControl);
			RemoveChild(advancedControlsIndex);
			tabControl.Close();

			tabControl = CreateTabControl();
			AddChild(tabControl, advancedControlsIndex);
			PopOutManager.SaveIfClosed = true;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private TabControl CreateTabControl()
		{
			var newTabControl = ApplicationController.Instance.Theme.CreateTabControl();
			newTabControl.TabBar.TabIndexChanged += (s, e) =>
			{
				ApplicationController.Instance.ActiveAdvancedControlsTab = newTabControl.SelectedTabIndex;
			};

			var libraryTabPage = new TabPage(new PrintLibraryWidget(), "Library".Localize().ToUpper());
			newTabControl.AddTab(new SimpleTextTabWidget(
				libraryTabPage, 
				"Library Tab",
				newTabControl.TextPointSize,
				ActiveTheme.Instance.TabLabelSelected, 
				new RGBA_Bytes(),
				ActiveTheme.Instance.TabLabelUnselected, 
				new RGBA_Bytes()));

			newTabControl.SelectedTabIndex = ApplicationController.Instance.ActiveAdvancedControlsTab;

			return newTabControl;
		}
	}
}