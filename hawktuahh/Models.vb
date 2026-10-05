Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Web.Script.Serialization

Public Enum DestKind
    PlainFolder = 0       ' just move the dropped items into a folder
    MsfsCommunity = 1     ' install mod packages directly into the Community folder
    MsfsAddonLinker = 2   ' store mod packages in an addons folder + link them into Community
End Enum

Public Class DestProfile
    Public Property Name As String = ""
    Public Property Kind As DestKind = DestKind.PlainFolder
    ''' <summary>Where files/mods are physically stored (destination, Community, or the AddonLinker addons folder).</summary>
    Public Property TargetPath As String = ""
    ''' <summary>AddonLinker only: the Community folder that receives the links.</summary>
    Public Property LinkPath As String = ""

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

Public Class AppSettings
    Public Property Profiles As New List(Of DestProfile)()
    Public Property LastProfile As String = ""
    Public Property KeepZip As Boolean = False

    Private Shared ReadOnly Dir As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModDropper")
    Private Shared ReadOnly SettingsFile As String = Path.Combine(Dir, "settings.json")
    Private Shared ReadOnly Json As New JavaScriptSerializer()

    Public Shared Function Load() As AppSettings
        Try
            If File.Exists(SettingsFile) Then
                Dim s As AppSettings = Json.Deserialize(Of AppSettings)(File.ReadAllText(SettingsFile))
                If s IsNot Nothing Then Return s
            End If
        Catch
        End Try
        Return New AppSettings()
    End Function

    Public Sub Save()
        Try
            Directory.CreateDirectory(Dir)
            File.WriteAllText(SettingsFile, Json.Serialize(Me))
        Catch
        End Try
    End Sub
End Class

Public Module MsfsLocator
    ''' <summary>
    ''' Best-effort: reads MSFS's UserCfg.opt files (Steam + Microsoft Store installs,
    ''' 2020 and 2024) and returns any Community folders that exist.
    ''' </summary>
    Public Function FindCommunityFolders() As List(Of String)
        Dim result As New List(Of String)()
        Dim roaming As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim local As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim cfgs As String() = {
            Path.Combine(roaming, "Microsoft Flight Simulator", "UserCfg.opt"),
            Path.Combine(local, "Packages", "Microsoft.FlightSimulator_8wekyb3d8bbwe", "LocalCache", "UserCfg.opt"),
            Path.Combine(roaming, "Microsoft Flight Simulator 2024", "UserCfg.opt"),
            Path.Combine(local, "Packages", "Microsoft.Limitless_8wekyb3d8bbwe", "LocalCache", "UserCfg.opt")
        }
        For Each cfg As String In cfgs
            If Not File.Exists(cfg) Then Continue For
            Dim root As String = ReadInstalledPackagesPath(cfg)
            If root Is Nothing Then Continue For
            Dim comm As String = Path.Combine(root, "Community")
            If Directory.Exists(comm) AndAlso Not result.Contains(comm) Then result.Add(comm)
        Next
        Return result
    End Function

    Private Function ReadInstalledPackagesPath(cfgFile As String) As String
        Try
            For Each line As String In File.ReadLines(cfgFile)
                Dim t As String = line.Trim()
                If t.StartsWith("InstalledPackagesPath", StringComparison.OrdinalIgnoreCase) Then
                    Dim q1 As Integer = t.IndexOf(""""c)
                    Dim q2 As Integer = t.LastIndexOf(""""c)
                    If q2 > q1 AndAlso q1 >= 0 Then Return t.Substring(q1 + 1, q2 - q1 - 1)
                    Return Nothing
                End If
            Next
        Catch
        End Try
        Return Nothing
    End Function
End Module
