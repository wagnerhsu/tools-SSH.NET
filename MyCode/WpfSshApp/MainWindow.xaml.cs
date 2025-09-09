using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace WpfSshApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private SshClient? _sshClient;

    public MainWindow(ILogger<MainWindow> logger)
    {
        _logger = logger;

        InitializeComponent();

        _logger.LogInformation("MainWindow initializing");

        // Example log and UI feedback
        try
        {
            _logger.LogDebug("Doing a quick startup task");
            // ...small demo work
            // MessageBox.Show("WPF SSH demo running. Check console or logs folder.", "WpfSshApp");
            _logger.LogInformation("Startup demo completed");
            StatusTextBox.Text = "Ready.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MainWindow startup");
            StatusTextBox.Text = "Error during startup: " + ex.Message;
        }
    }

    private void SetStatus(string text)
    {
        // Replace content for short messages, append for command output
        StatusTextBox.Text = text;
    }

    private void AppendStatus(string text)
    {
        StatusTextBox.Text = string.IsNullOrEmpty(StatusTextBox.Text) ? text : StatusTextBox.Text + "\n" + text;
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var host = HostTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(host))
        {
            SetStatus("Please enter a host.");
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out var port))
        {
            SetStatus("Invalid port number.");
            return;
        }

        var username = UserNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            SetStatus("Please enter a username.");
            return;
        }

        var password = PasswordBox.Password ?? string.Empty;

        try
        {
            _sshClient?.Dispose();
            _sshClient = new SshClient(host, port, username, password);
            _sshClient.Connect();

            if (_sshClient.IsConnected)
            {
                SetStatus($"Connected to {host}:{port} as {username}.");
                _logger.LogInformation("Connected to {Host}:{Port} as {User}", host, port, username);
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                ExecuteButton.IsEnabled = true;
            }
            else
            {
                SetStatus("Failed to connect.");
                _logger.LogWarning("Failed to connect to {Host}:{Port}", host, port);
            }
        }
        catch (Exception ex)
        {
            SetStatus("Connect error: " + ex.Message);
            _logger.LogError(ex, "Connect error");
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_sshClient != null && _sshClient.IsConnected)
            {
                _sshClient.Disconnect();
                SetStatus("Disconnected.");
                _logger.LogInformation("Disconnected from SSH server");
            }
            _sshClient?.Dispose();
            _sshClient = null;
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            ExecuteButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            SetStatus("Disconnect error: " + ex.Message);
            _logger.LogError(ex, "Disconnect error");
        }
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        var cmdText = CommandTextBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cmdText))
        {
            SetStatus("Please enter a command to execute.");
            return;
        }

        if (_sshClient == null || !_sshClient.IsConnected)
        {
            SetStatus("Not connected.");
            return;
        }

        ExecuteButton.IsEnabled = false;
        try
        {
            SetStatus("Executing...");
            _logger.LogInformation("Executing command: {Cmd}", cmdText);

            // Run command in background to avoid blocking UI thread
            var result = await Task.Run(() =>
            {
                try
                {
                    using var command = _sshClient.CreateCommand(cmdText);
                    // Execute synchronously on background thread
                    return command.Execute();
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            });

            AppendStatus(result);
            _logger.LogInformation("Command result: {Result}", result);
        }
        catch (Exception ex)
        {
            SetStatus("Execute error: " + ex.Message);
            _logger.LogError(ex, "Execute error");
        }
        finally
        {
            ExecuteButton.IsEnabled = true;
        }
    }
}
