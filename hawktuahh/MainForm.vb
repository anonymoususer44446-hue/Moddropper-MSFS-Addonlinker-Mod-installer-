Imports System.Threading.Tasks

Public Class MainForm
    Inherits Form

    Private ReadOnly _settings As AppSettings = AppSettings.Load()
    Private ReadOnly _profiles As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 280}
    Private ReadOnly _add As New Button() With {.Text = "Add", .AutoSize = True, .MinimumSize = New Size(80, 30)}
    Private ReadOnly _edit As New Button() With {.Text = "Edit", .AutoSize = True, .MinimumSize = New Size(80, 30)}
    Private ReadOnly _remove As New Button() With {.Text = "Remove", .AutoSize = True, .MinimumSize = New Size(80, 30)}
    Private ReadOnly _info As New Label() With {.Dock = DockStyle.Top, .AutoSize = False, .Height = 22, .ForeColor = Theme.MutedColor, .Tag = "keep"}
    Private ReadOnly _dropBox As New Label() With {
        .Dock = DockStyle.Fill, .AllowDrop = True, .TextAlign = ContentAlignment.MiddleCenter,
        .BackColor = Theme.PanelColor, .ForeColor = Theme.MutedColor, .Tag = "keep",
        .Font = New Font("Segoe UI", 14.0F), .Text = "Drop mod folders or archives (.zip .7z .rar .tar) here"}
    Private ReadOnly _keepZip As New CheckBox() With {.Text = "Keep the archive after installing", .Dock = DockStyle.Top, .AutoSize = False, .Height = 32, .Padding = New Padding(4, 4, 0, 4)}
    Private ReadOnly _log As New ListBox() With {.Dock = DockStyle.Fill, .HorizontalScrollbar = True}
    Private ReadOnly _progress As New DarkProgressBar() With {.Dock = DockStyle.Bottom, .Height = 20}
    Private ReadOnly _status As New Label() With {.Dock = DockStyle.Bottom, .AutoSize = False, .Height = 24, .ForeColor = Theme.MutedColor, .Tag = "keep", .TextAlign = ContentAlignment.BottomLeft}
    Private ReadOnly _spacer As New System.Windows.Forms.Panel() With {.Dock = DockStyle.Bottom, .Height = 10}
    Private ReadOnly _onTop As New CheckBox() With {.Text = "Stay on top", .Dock = DockStyle.Top, .AutoSize = False, .Height = 32, .Padding = New Padding(4, 4, 0, 4)}
    Private _hot As Boolean
    Private _lastPct As Integer = -1
    Private _lastText As String = ""

    Public Sub New()
        Text = "Mod Dropper"
        ClientSize = New Size(780, 580)
        MinimumSize = New Size(760, 560)
        Padding = New Padding(16)

        Dim top As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .AutoSize = True, .WrapContents = False}
        top.Controls.AddRange(New Control() {New Label() With {.Text = "Destination:", .AutoSize = True, .Padding = New Padding(0, 6, 0, 0)},
                                             _profiles, _add, _edit, _remove})

        Dim logPanel As New Panel() With {.Dock = DockStyle.Bottom, .Height = 140}
        logPanel.Controls.Add(_log)

        Controls.Add(_dropBox)
        Controls.Add(_progress)
        Controls.Add(_status)
        Controls.Add(_spacer)
        Controls.Add(logPanel)
        Controls.Add(_onTop)
        Controls.Add(_keepZip)
        Controls.Add(_info)
        Controls.Add(top)

        _keepZip.Checked = _settings.KeepZip
        SeedDefaults()
        RefreshProfiles(_settings.LastProfile)

        AddHandler _profiles.SelectedIndexChanged, Sub() UpdateInfo()
        AddHandler _onTop.CheckedChanged, Sub() TopMost = _onTop.Checked
        AddHandler _add.Click, Sub() AddProfile()
        AddHandler _edit.Click, Sub() EditProfile()
        AddHandler _remove.Click, Sub() RemoveProfile()
        AddHandler _dropBox.DragEnter, AddressOf HandleDragEnter
        AddHandler _dropBox.DragLeave, Sub() SetHot(False)
        AddHandler _dropBox.Paint, AddressOf PaintDropBox
        Theme.Apply(Me)
        _progress.Visible = False
        _status.Text = "Ready"
        AddHandler _dropBox.DragDrop, AddressOf HandleDragDrop
        AddHandler FormClosing, Sub() SaveSettings()
    End Sub

    Private Sub SeedDefaults()
        If _settings.Profiles.Count > 0 Then Return
        Dim n As Integer = 0
        For Each comm As String In MsfsLocator.FindCommunityFolders()
            n += 1
            _settings.Profiles.Add(New DestProfile() With {
                .Name = If(n = 1, "MSFS Community", "MSFS Community " & n),
                .Kind = DestKind.MsfsCommunity,
                .TargetPath = comm})
        Next
    End Sub

    Private Sub RefreshProfiles(selectName As String)
        _profiles.Items.Clear()
        For Each p As DestProfile In _settings.Profiles
            _profiles.Items.Add(p)
        Next
        Dim idx As Integer = _settings.Profiles.FindIndex(Function(p) p.Name = selectName)
        If idx < 0 AndAlso _profiles.Items.Count > 0 Then idx = 0
        _profiles.SelectedIndex = idx
        UpdateInfo()
    End Sub

    Private Function Current() As DestProfile
        Return TryCast(_profiles.SelectedItem, DestProfile)
    End Function

    Private Sub UpdateInfo()
        Dim p As DestProfile = Current()
        _edit.Enabled = p IsNot Nothing
        _remove.Enabled = p IsNot Nothing
        If p Is Nothing Then
            _info.Text = "Add a destination to get started."
            Return
        End If
        Select Case p.Kind
            Case DestKind.PlainFolder
                _info.Text = "Moves dropped items into: " & p.TargetPath
            Case DestKind.MsfsCommunity
                _info.Text = "Installs mods directly into: " & p.TargetPath
            Case Else
                _info.Text = "Stores mods in " & p.TargetPath & " and links them into " & p.LinkPath
        End Select
    End Sub

    Private Sub SaveSettings()
        _settings.KeepZip = _keepZip.Checked
        Dim p As DestProfile = Current()
        If p IsNot Nothing Then _settings.LastProfile = p.Name
        _settings.Save()
    End Sub

    Private Function NameTaken(name As String, except As DestProfile) As Boolean
        Return _settings.Profiles.Any(Function(x) x IsNot except AndAlso String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub AddProfile()
        Using dlg As New ProfileDialog(Nothing)
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If NameTaken(dlg.Profile.Name, Nothing) Then
                MessageBox.Show(Me, "A destination with that name already exists.", Text)
                Return
            End If
            _settings.Profiles.Add(dlg.Profile)
            SaveSettings()
            RefreshProfiles(dlg.Profile.Name)
        End Using
    End Sub

    Private Sub EditProfile()
        Dim old As DestProfile = Current()
        If old Is Nothing Then Return
        Using dlg As New ProfileDialog(old)
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If NameTaken(dlg.Profile.Name, old) Then
                MessageBox.Show(Me, "A destination with that name already exists.", Text)
                Return
            End If
            _settings.Profiles(_settings.Profiles.IndexOf(old)) = dlg.Profile
            SaveSettings()
            RefreshProfiles(dlg.Profile.Name)
        End Using
    End Sub

    Private Sub RemoveProfile()
        Dim p As DestProfile = Current()
        If p Is Nothing Then Return
        If MessageBox.Show(Me, $"Remove destination '{p.Name}'? Files already installed are not touched.", Text,
                           MessageBoxButtons.YesNo) <> DialogResult.Yes Then Return
        _settings.Profiles.Remove(p)
        SaveSettings()
        RefreshProfiles("")
    End Sub

    Private Sub HandleDragEnter(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
            SetHot(True)
        End If
    End Sub

    Private Sub HandleDragDrop(sender As Object, e As DragEventArgs)
        SetHot(False)
        Dim profile As DestProfile = Current()
        If profile Is Nothing Then
            AddLog("Add a destination first.")
            Return
        End If
        Dim items As String() = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        Dim installer As New ModInstaller(
            AddressOf AddLog,
            Function(q) CBool(Invoke(New Func(Of Boolean)(Function() MessageBox.Show(Me, q, Text, MessageBoxButtons.YesNo) = DialogResult.Yes))),
            _keepZip.Checked)
        installer.Progress = AddressOf ReportProgress
        _lastPct = -1
        _lastText = ""
        SetBusy(True)
        AddLog($"--- {profile.Name} ---")
        Task.Run(Sub()
                     Try
                         installer.Install(items, profile)
                     Catch ex As Exception
                         AddLog("ERROR: " & ex.Message)
                     Finally
                         BeginInvoke(New Action(Sub() SetBusy(False)))
                     End Try
                 End Sub)
    End Sub

    Private Sub SetBusy(busy As Boolean)
        _dropBox.AllowDrop = Not busy
        _profiles.Enabled = Not busy
        _add.Enabled = Not busy
        _edit.Enabled = Not busy AndAlso Current() IsNot Nothing
        _remove.Enabled = Not busy AndAlso Current() IsNot Nothing
        _dropBox.Text = If(busy, "Working...", "Drop mod folders or archives (.zip .7z .rar .tar) here")
        _progress.Visible = busy
        If busy Then
            _progress.Value = 0
        Else
            _status.Text = "Done"
        End If
    End Sub

    Private Sub ReportProgress(pct As Integer, text As String)
        If pct = _lastPct AndAlso text = _lastText Then Return
        _lastPct = pct
        _lastText = text
        BeginInvoke(New Action(Sub()
                                   _progress.Value = pct
                                   _status.Text = text
                               End Sub))
    End Sub

    Private Sub SetHot(hot As Boolean)
        _hot = hot
        _dropBox.BackColor = If(hot, Theme.SurfaceColor, Theme.PanelColor)
        _dropBox.Invalidate()
    End Sub

    Private Sub PaintDropBox(sender As Object, e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Dim r As New Rectangle(6, 6, _dropBox.Width - 13, _dropBox.Height - 13)
        Using path As Drawing2D.GraphicsPath = Theme.RoundRect(r, 14),
              pen As New Pen(If(_hot, Theme.AccentColor, Theme.BorderColor), 2) With {.DashStyle = Drawing2D.DashStyle.Dash}
            e.Graphics.DrawPath(pen, path)
        End Using
    End Sub

    Private Sub AddLog(msg As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Sub() AddLog(msg)))
            Return
        End If
        _log.Items.Add(msg)
        _log.TopIndex = _log.Items.Count - 1
    End Sub
End Class
