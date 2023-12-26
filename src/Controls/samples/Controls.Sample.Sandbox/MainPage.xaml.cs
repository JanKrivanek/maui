using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace Maui.Controls.Sample
{
	public partial class MainPage : ContentPage
	{
		public MainPage()
		{
			InitializeComponent();
			appButton.Clicked += appButton_Clicked;
			windowButton.Clicked += WindowButton_Clicked;
		}

		private void WindowButton_Clicked(object? sender, EventArgs e)
		{
			Task.Run(() =>
			{
				var dispatch = this.Handler!.MauiContext!.Services.GetService<IDispatcher>()!;

				dispatch.Dispatch(() =>
				{

				});
			});
		}

		private void appButton_Clicked(object? sender, EventArgs e)
		{
			Task.Run(() =>
			{
				var dispatch = MauiProgram.MauiApp2!.Services.GetService<IDispatcher>()!;

				dispatch.Dispatch(() =>
				{

				});
			});
		}
	}
}