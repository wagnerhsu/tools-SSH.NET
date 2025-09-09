using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
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
        // Scroll to end
        StatusTextBox.ScrollToEnd();
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

    // Streams command output as async enumerable chunks
    private async IAsyncEnumerable<string> StreamCommandOutputAsync(SshCommand command, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new byte[4096];
        var outputStream = command.OutputStream;

        // Start execution
        var execTask = command.ExecuteAsync(cancellationToken);

        while (true)
        {
            int read;
            try
            {
                read = await outputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (read > 0)
            {
                yield return Encoding.UTF8.GetString(buffer, 0, read);
            }
            else
            {
                // no data currently available
                if (execTask.IsCompleted)
                {
                    // try one last read to drain any remaining data
                    read = await outputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read > 0)
                    {
                        yield return Encoding.UTF8.GetString(buffer, 0, read);
                    }

                    break;
                }

                // wait briefly before trying again
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        // await execution to observe exceptions
        await execTask.ConfigureAwait(false);

        // include stderr and exit status
        if (!string.IsNullOrEmpty(command.Error))
        {
            yield return "\n[stderr] " + command.Error;
        }

        if (command.ExitStatus.HasValue)
        {
            yield return $"\n[exit code] {command.ExitStatus.Value}";
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

            using var command = _sshClient.CreateCommand(cmdText);
            // Set a timeout to avoid hanging indefinitely (30s)
            command.CommandTimeout = TimeSpan.FromSeconds(30);

            var cts = new CancellationTokenSource();

            await foreach (var chunk in StreamCommandOutputAsync(command, cts.Token))
            {
                // update UI progressively
                AppendStatus(chunk);
            }

            _logger.LogInformation("Command completed: {Cmd}", cmdText);
        }
        catch (Exception ex)
        {
            SetStatus("Execute error: " + ex.Message);
            _logger.LogError(ex, "Execute error");
        }
        finally
        {
            StatusTextBox.ScrollToEnd();
            ExecuteButton.IsEnabled = true;
        }
    }
}
