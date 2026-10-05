Imports System.IO
Imports SharpCompress.Readers
Imports SharpCompress.Archives

Public Module ArchiveExtractor
    Private ReadOnly Exts As String() = {".zip", ".7z", ".rar", ".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tbz2", ".tar.xz", ".txz"}
    Public Function IsArchive(p As String) As Boolean
        Return Exts.Any(Function(x) p.EndsWith(x, StringComparison.OrdinalIgnoreCase))
    End Function
    Public Function IsZip(p As String) As Boolean
        Return p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
    End Function
    Public Function BaseName(p As String) As String
        Dim nm As String = Path.GetFileName(p)
        For Each x As String In Exts.OrderByDescending(Function(e) e.Length)
            If nm.EndsWith(x, StringComparison.OrdinalIgnoreCase) Then Return nm.Substring(0, nm.Length - x.Length)
        Next
        Return Path.GetFileNameWithoutExtension(p)
    End Function
    Public Sub Extract(archivePath As String, destRoot As String, report As Action(Of Double))
        Directory.CreateDirectory(LongPath.ToLong(destRoot))
        Dim rootFull As String = Path.GetFullPath(destRoot).TrimEnd("\"c) & "\"
        Using fs As New FileStream(LongPath.ToLong(archivePath), FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim total As Double = Math.Max(1L, fs.Length)
            Using arc As IArchive = If(archivePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase), ArchiveFactory.OpenArchive(fs), Nothing)
            Using reader As IReader = If(arc IsNot Nothing, arc.ExtractAllEntries(), ReaderFactory.OpenReader(fs))
                While reader.MoveToNextEntry()
                    Dim entry = reader.Entry
                    If entry.Key Is Nothing Then Continue While
                    Dim rel As String = entry.Key.Replace("/"c, "\"c)
                    Dim dest As String = Path.GetFullPath(Path.Combine(destRoot, rel))
                    If Not dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) Then Continue While
                    If entry.IsDirectory Then
                        Directory.CreateDirectory(LongPath.ToLong(dest))
                        Continue While
                    End If
                    LongPath.EnsureParent(dest)
                    Using src As Stream = reader.OpenEntryStream()
                        Using dst As New FileStream(LongPath.ToLong(dest), FileMode.Create, FileAccess.Write)
                            Dim buf(81919) As Byte
                            Dim n As Integer = src.Read(buf, 0, buf.Length)
                            While n > 0
                                dst.Write(buf, 0, n)
                                report(fs.Position / total)
                                n = src.Read(buf, 0, buf.Length)
                            End While
                        End Using
                    End Using
                End While
            End Using
            End Using
        End Using
    End Sub
End Module
