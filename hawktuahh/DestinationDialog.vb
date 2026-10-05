Imports System.IO

Public Class ProfileDialog
    Inherits Form

    Private ReadOnly _name As New TextBox() With {.Left = 120, .Top = 15, .Width = 380}
    Private ReadOnly _kind As New ComboBox() With {.Left = 120, .Top = 50, .Width = 380, .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly _target As New TextBox() With {.Left = 120, .Top = 85, .Width = 270}
    Private ReadOnly _link As New TextBox() With {.Left = 120, .Top = 120, .Width = 270}
    Private ReadOnly _targetLbl As New Label() With {.Left = 10, .Top = 88, .Width = 110}
    Private ReadOnly _linkLbl As New Label() With {.Left = 10, .Top = 123, .Width = 110, .Text = "Community folder"}
    Private ReadOnly _browseTarget As New Button() With {.Left = 400, .Top = 82, .Width = 100, .Height = 28, .Text = "Browse..."}
    Private ReadOnly _browseLink As New Button() With {.Left = 400, .Top = 117, .Width = 100, .Height = 28, .Text = "Browse..."}
    Private ReadOnly _detect As New Button() With {.Left = 120, .Top = 158, .Width = 260, .Height = 28, .Text = "Auto-detect MSFS Community"}
    Private ReadOnly _ok As New Button() With {.Left = 290, .Top = 205, .Width = 100, .Height = 30, .Text = "OK", .DialogResult = DialogResult.OK}
    Private ReadOnly _cancel As New Button() With {.Left = 400, .Top = 205, .Width = 100, .Height = 30, .Text = "Cancel", .DialogResult = DialogResult.Cancel}

    Public Property Profile As DestProfile

    Public Sub New(existing As DestProfile)
        Text = If(existing Is Nothing, "Add destination", "Edit destination")
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(520, 255)
        AcceptButton = _ok
        CancelButton = _cancel

        Controls.Add(New Label() With {.Left = 10, .Top = 18, .Width = 110, .Text = "Name"})
        Controls.Add(New Label() With {.Left = 10, .Top = 53, .Width = 110, .Text = "Type"})
        Controls.AddRange(New Control() {_name, _kind, _target, _link, _targetLbl, _linkLbl, _browseTarget, _browseLink, _detect, _ok, _cancel})

        _kind.Items.Add("Plain folder (just move files)")
        _kind.Items.Add("MSFS Community folder (install directly)")
        _kind.Items.Add("MSFS AddonLinker (store + link into Community)")

        Dim src As DestProfile = If(existing, New DestProfile())
        _name.Text = src.Name
        _kind.SelectedIndex = CInt(src.Kind)
        _target.Text = src.TargetPath
        _link.Text = src.LinkPath

        AddHandler _kind.SelectedIndexChanged, Sub() UpdateLayoutForKind()
        AddHandler _browseTarget.Click, Sub() Browse(_target)
        AddHandler _browseLink.Click, Sub() Browse(_link)
        AddHandler _detect.Click, AddressOf OnDetect
        AddHandler _ok.Click, AddressOf OnOk
        UpdateLayoutForKind()
        Theme.Apply(Me)
    End Sub

    Private Function CurrentKind() As DestKind
        Return CType(_kind.SelectedIndex, DestKind)
    End Function

    Private Sub UpdateLayoutForKind()
        Dim k As DestKind = CurrentKind()
        _targetLbl.Text = If(k = DestKind.MsfsAddonLinker, "Addons folder",
                          If(k = DestKind.MsfsCommunity, "Community folder", "Destination"))
        Dim linker As Boolean = (k = DestKind.MsfsAddonLinker)
        _link.Visible = linker
        _linkLbl.Visible = linker
        _browseLink.Visible = linker
        _detect.Visible = (k <> DestKind.PlainFolder)
    End Sub

    Private Sub Browse(box As TextBox)
        Dim picked As String = ExplorerFolderPicker.Pick(Me, box.Text, "Select folder")
        If picked IsNot Nothing Then box.Text = picked
    End Sub

    Private Sub OnDetect(sender As Object, e As EventArgs)
        Dim found As List(Of String) = MsfsLocator.FindCommunityFolders()
        If found.Count = 0 Then
            MessageBox.Show(Me, "Couldn't find an MSFS Community folder. Please browse for it.", Text)
            Return
        End If
        Dim pick As String = found(0)
        If found.Count > 1 Then
            Dim msg As String = "Several Community folders were found. Use the first one?" & vbCrLf & vbCrLf & String.Join(vbCrLf, found)
            If MessageBox.Show(Me, msg, Text, MessageBoxButtons.YesNo) <> DialogResult.Yes Then Return
        End If
        If CurrentKind() = DestKind.MsfsAddonLinker Then _link.Text = pick Else _target.Text = pick
    End Sub

    Private Sub OnOk(sender As Object, e As EventArgs)
        Dim k As DestKind = CurrentKind()
        Dim problem As String = Nothing
        If _name.Text.Trim() = "" Then
            problem = "Enter a name."
        ElseIf _target.Text.Trim() = "" Then
            problem = "Choose a folder."
        ElseIf k = DestKind.MsfsAddonLinker AndAlso _link.Text.Trim() = "" Then
            problem = "Choose the Community folder that receives the links."
        End If
        If problem IsNot Nothing Then
            MessageBox.Show(Me, problem, Text)
            DialogResult = DialogResult.None
            Return
        End If
        Profile = New DestProfile() With {
            .Name = _name.Text.Trim(),
            .Kind = k,
            .TargetPath = _target.Text.Trim(),
            .LinkPath = If(k = DestKind.MsfsAddonLinker, _link.Text.Trim(), "")
        }
    End Sub
End Class
