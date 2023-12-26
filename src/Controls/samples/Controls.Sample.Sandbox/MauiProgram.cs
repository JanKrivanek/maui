using Microsoft.Extensions.DependencyInjection;
using System.Reflection.PortableExecutable;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace Maui.Controls.Sample
{
	public static class MauiProgram
	{
		public static MauiApp? MauiApp2 { get; set; }

		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp
			.CreateBuilder()
			.UseMauiMaps()
				.UseMauiApp<App>();
			builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
			{
				ValidateOnBuild = true,
				ValidateScopes = true,
			}));

			var boop2 = builder.Build();

			MauiApp2 = boop2;

			return boop2;
		}
	}

	class App : Application
	{
		protected override Window CreateWindow(IActivationState? activationState)
		{
			// To test shell scenarios, change this to true
			bool useShell = false;

			if (!useShell)
			{
				return new Window(new NavigationPage(new MainPage()));
			}
			else
			{
				return new Window(new SandboxShell());
			}
		}
	}
}