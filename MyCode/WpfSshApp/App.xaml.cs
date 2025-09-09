using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WpfSshApp;

/// <summary>
/// Host-based WPF application entry.
/// </summary>
public partial class App : Application
{
	private IHost? _host;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// Build host with default configuration and Serilog
		_host = Host.CreateDefaultBuilder()
			.UseSerilog((context, services, configuration) =>
			{
				configuration
					.MinimumLevel.Debug()
					.WriteTo.Console()
					.WriteTo.File("logs\\WpfSshApp-.log", rollingInterval: RollingInterval.Day);
			})
			.ConfigureServices((context, services) =>
			{
				// Register WPF windows and app services
				services.AddTransient<MainWindow>();
			})
			.Build();

		await _host.StartAsync();

		var mainWindow = _host.Services.GetRequiredService<MainWindow>();
		mainWindow.Show();
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		if (_host != null)
		{
			await _host.StopAsync(TimeSpan.FromSeconds(5));
			_host.Dispose();
		}
		Log.CloseAndFlush();
		base.OnExit(e);
	}
}

