Imports System.IO
Imports System.IO.Compression

''' <summary>
''' Does the actual work for a drop. Runs on a background thread, so it only talks to the UI
''' through the two callbacks it is given (log line, yes/no question).
''' </summary>
Public Class ModInstaller

    Private ReadOnly _log As Action(Of String)
    Private ReadOnly _confirm As Func(Of String, Boolean)
    Private ReadOnly _keepZip As Boolean
    Private _itemIndex As Integer
    Private _itemCount As Integer

    ''' <summary>Optional. Receives overall percent (0-100) and a status text.</summary>
    Public Property Progress As Action(Of Integer, String)

    Private Sub Report(fraction As Double, text As String)
        If Progress Is Nothing Then Return
        Dim overall As Double = (_itemIndex + Math.Min(1.0, Math.Max(0.0, fraction))) / Math.Max(1, _itemCount)
        Progress.Invoke(CInt(overall * 100), text)
    End Sub

    Public Sub New(log As Action(Of String), confirm As Func(Of String, Boolean), keepZip As Boolean)
        _log = log
        _confirm = confirm
        _keepZip = keepZip
    End Sub

    Public Sub Install(items As IEnumerable(Of String), profile As DestProfile)
        Dim list As List(Of String) = items.ToList()
        _itemCount = list.Count
        _itemIndex = 0
        For Each item As String In list
            Report(0, LongPath.LeafName(item))
            Try
                If profile.Kind = DestKind.PlainFolder Then
                    InstallPlain(item, profile)
                Else
                    InstallMsfs(item, profile)
                End If
            Catch ex As Exception
                _log($"ERROR  {LongPath.LeafName(item)}: {ex.Message}")
            End Try
            _itemIndex += 1
            Report(0, LongPath.LeafName(item))
        Next
        _log("Done.")
    End Sub

    ' ---------------------------------------------------------------- plain folder

    Private Sub InstallPlain(item As String, profile As DestProfile)
        Dim name As String = LongPath.LeafName(item)
        Dim dest As String = Path.Combine(profile.TargetPath, name)
        Directory.CreateDirectory(LongPath.ToLong(profile.TargetPath))

        If LongPath.AnyExists(dest) Then
            If Not _confirm($"'{name}' already exists in {profile.TargetPath}. Replace it?") Then
                _log($"Skipped {name}")
                Return
            End If
            LongPath.DeleteAny(dest)
        End If

        If Directory.Exists(LongPath.ToLong(item)) Then
            LongPath.MoveDir(item, dest)
        Else
            LongPath.MoveFile(item, dest)
        End If
        _log($"Moved {name} -> {profile.TargetPath}")
    End Sub

    ' ---------------------------------------------------------------- MSFS mods

    Private Sub InstallMsfs(item As String, profile As DestProfile)
        Dim itemLong As String = LongPath.ToLong(item)
        Dim sourceRoot As String = Nothing
        Dim fallbackName As String = Nothing
        Dim staging As String = Nothing
        Dim isZip As Boolean = ArchiveExtractor.IsArchive(item) AndAlso File.Exists(itemLong)

        Try
            If isZip Then
                Directory.CreateDirectory(LongPath.ToLong(profile.TargetPath))
                ' Staging lives next to the final location so the final move is an instant rename.
                staging = Path.Combine(profile.TargetPath, ".moddropper-" & Guid.NewGuid().ToString("N").Substring(0, 8))
                _log($"Extracting {LongPath.LeafName(item)} ...")
                If ArchiveExtractor.IsZip(item) Then ExtractZip(item, staging) Else ArchiveExtractor.Extract(item, staging, Sub(fr) Report(fr, "Extracting " & LongPath.LeafName(item)))
                sourceRoot = staging
                fallbackName = ArchiveExtractor.BaseName(item)
            ElseIf Directory.Exists(itemLong) Then
                sourceRoot = item
                fallbackName = LongPath.LeafName(item)
            Else
                _log($"Skipped {LongPath.LeafName(item)}: MSFS destinations accept folders and archives (.zip .7z .rar .tar .tar.gz) only.")
                Return
            End If

            Dim pkgs As List(Of KeyValuePair(Of String, String)) = FindPackages(sourceRoot, fallbackName)
            If pkgs.Count = 0 Then
                If Not _confirm($"No manifest.json found in '{fallbackName}'. Install it as a single folder anyway?") Then
                    _log($"Skipped {fallbackName} (no manifest.json)")
                    Return
                End If
                pkgs.Add(New KeyValuePair(Of String, String)(fallbackName, sourceRoot))
            End If

            For Each pkg As KeyValuePair(Of String, String) In pkgs
                PlacePackage(pkg.Value, pkg.Key, profile)
            Next

            If isZip AndAlso Not _keepZip Then
                File.SetAttributes(itemLong, FileAttributes.Normal)
                File.Delete(itemLong)
            End If
        Finally
            If staging IsNot Nothing AndAlso Directory.Exists(LongPath.ToLong(staging)) Then
                Try
                    LongPath.DeleteAny(staging)
                Catch
                End Try
            End If
        End Try
    End Sub

    ''' <summary>
    ''' A mod package is a folder containing manifest.json. If the root itself has one, the whole
    ''' root is one package (named after the zip/folder); otherwise look up to 3 levels down.
    ''' </summary>
    Private Function FindPackages(root As String, fallbackName As String) As List(Of KeyValuePair(Of String, String))
        Dim result As New List(Of KeyValuePair(Of String, String))()
        If File.Exists(LongPath.ToLong(Path.Combine(root, "manifest.json"))) Then
            result.Add(New KeyValuePair(Of String, String)(fallbackName, root))
        Else
            Scan(root, 0, result)
        End If
        Return result
    End Function

    Private Sub Scan(folder As String, depth As Integer, result As List(Of KeyValuePair(Of String, String)))
        For Each child As String In Directory.GetDirectories(LongPath.ToLong(folder))
            If File.Exists(Path.Combine(child, "manifest.json")) Then
                result.Add(New KeyValuePair(Of String, String)(LongPath.LeafName(child), child))
            ElseIf depth < 2 Then
                Scan(child, depth + 1, result)
            End If
        Next
    End Sub

    Private Sub PlacePackage(src As String, name As String, profile As DestProfile)
        Directory.CreateDirectory(LongPath.ToLong(profile.TargetPath))
        Dim dest As String = Path.Combine(profile.TargetPath, name)

        If LongPath.AnyExists(dest) Then
            If Not _confirm($"'{name}' already exists in {profile.TargetPath}. Replace it?") Then
                _log($"Skipped {name}")
                Return
            End If
            LongPath.DeleteAny(dest)
        End If

        LongPath.MoveDir(src, dest)
        _log($"Installed {name} -> {profile.TargetPath}")

        If profile.Kind = DestKind.MsfsAddonLinker Then
            LinkIntoCommunity(name, dest, profile.LinkPath)
        End If
    End Sub

    Private Sub LinkIntoCommunity(name As String, target As String, communityPath As String)
        Directory.CreateDirectory(LongPath.ToLong(communityPath))
        Dim linkPath As String = Path.Combine(communityPath, name)

        If LongPath.AnyExists(linkPath) Then
            If LongPath.IsLink(linkPath) Then
                LongPath.DeleteAny(linkPath) ' old/stale link - safe, target is untouched
            ElseIf _confirm($"A real folder named '{name}' already exists in the Community folder. Replace it with a link?") Then
                LongPath.DeleteAny(linkPath)
            Else
                _log($"Skipped link for {name}")
                Return
            End If
        End If

        Dim method As String = LinkHelper.CreateDirectoryLink(linkPath, target)
        _log($"Linked {name} into Community ({method})")
    End Sub

    ' ---------------------------------------------------------------- zip

    Private Sub ExtractZip(zipPath As String, destRoot As String)
        Directory.CreateDirectory(LongPath.ToLong(destRoot))
        Dim rootFull As String = Path.GetFullPath(destRoot).TrimEnd("\"c) & "\"

        Using zip As ZipArchive = ZipFile.OpenRead(LongPath.ToLong(zipPath))
            Dim totalBytes As Long = Math.Max(1L, zip.Entries.Sum(Function(x) x.Length))
            Dim doneBytes As Long = 0
            Dim label As String = "Extracting " & LongPath.LeafName(zipPath)
            For Each entry As ZipArchiveEntry In zip.Entries
                Dim rel As String = entry.FullName.Replace("/"c, "\"c)
                Dim dest As String = Path.GetFullPath(Path.Combine(destRoot, rel))

                ' zip-slip guard: never write outside the staging folder
                If Not dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) Then Continue For

                If rel.EndsWith("\", StringComparison.Ordinal) Then
                    Directory.CreateDirectory(LongPath.ToLong(dest))
                Else
                    LongPath.EnsureParent(dest)
                    Using src As Stream = entry.Open()
                        Using dst As New FileStream(LongPath.ToLong(dest), FileMode.Create, FileAccess.Write)
                            Dim buf(81919) As Byte
                            Dim n As Integer = src.Read(buf, 0, buf.Length)
                            While n > 0
                                dst.Write(buf, 0, n)
                                doneBytes += n
                                Report(doneBytes / totalBytes, label)
                                n = src.Read(buf, 0, buf.Length)
                            End While
                        End Using
                    End Using
                End If
            Next
        End Using
    End Sub

End Class
