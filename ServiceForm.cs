using System.Drawing;
using System.Windows.Forms;

namespace QPrintBridge;

public sealed class ServiceForm : Form
{
    private readonly Label _statusLabel;
    private readonly Label _adminWarningLabel;
    private readonly Button _installButton;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _uninstallButton;

    public ServiceForm()
    {
        Text = "QPrintBridge - Gestor de Servicio";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 300);

        var title = new Label
        {
            Text = "QPrintBridge",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        var subtitle = new Label
        {
            Text = "Puente HTTP local → impresoras Windows (puerto 19100)",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(22, 52)
        };

        _statusLabel = new Label
        {
            Text = "Consultando estado...",
            AutoSize = true,
            Location = new Point(22, 92)
        };

        _installButton = new Button { Text = "Instalar servicio", Size = new Size(170, 34), Location = new Point(20, 130) };
        _startButton = new Button { Text = "Iniciar", Size = new Size(170, 34), Location = new Point(210, 130) };
        _stopButton = new Button { Text = "Detener", Size = new Size(170, 34), Location = new Point(20, 174) };
        _uninstallButton = new Button { Text = "Eliminar servicio", Size = new Size(170, 34), Location = new Point(210, 174) };

        _adminWarningLabel = new Label
        {
            Text = "Se requieren permisos de administrador para gestionar el servicio.",
            ForeColor = Color.Firebrick,
            AutoSize = true,
            Location = new Point(22, 226),
            MaximumSize = new Size(360, 0)
        };

        Controls.AddRange(new Control[] { title, subtitle, _statusLabel, _installButton, _startButton, _stopButton, _uninstallButton, _adminWarningLabel });

        _installButton.Click += (_, _) => Run("Instalando servicio...", ServiceManager.Install, "Servicio instalado correctamente.");
        _startButton.Click += (_, _) => Run("Iniciando servicio...", ServiceManager.Start, "Servicio iniciado.");
        _stopButton.Click += (_, _) => Run("Deteniendo servicio...", ServiceManager.Stop, "Servicio detenido.");
        _uninstallButton.Click += (_, _) => Run("Eliminando servicio...", ServiceManager.Uninstall, "Servicio eliminado correctamente.");

        Shown += (_, _) =>
        {
            RefreshStatus();
            if (!ServiceManager.IsAdministrator())
            {
                var result = MessageBox.Show(
                    "Se requieren permisos de administrador para instalar o eliminar el servicio. ¿Desea reiniciar la aplicación como administrador?",
                    "QPrintBridge",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    ServiceManager.RestartElevated();
                    Close();
                }
            }
        };
    }

    private async void Run(string working, Action action, string success)
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = working;
            await Task.Run(action);
            RefreshStatus();
            MessageBox.Show(success, "QPrintBridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            RefreshStatus();
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RefreshStatus()
    {
        var status = ServiceManager.GetStatus();

        bool admin = ServiceManager.IsAdministrator();
        bool installed = status != ServiceState.Unknown;
        bool running = status == ServiceState.Running;

        _statusLabel.Text = $"Estado del servicio: {status}";
        _adminWarningLabel.Visible = !admin;

        _installButton.Enabled = admin && !installed;
        _startButton.Enabled = admin && installed && !running;
        _stopButton.Enabled = admin && installed && running;
        _uninstallButton.Enabled = admin && installed;
    }
}
