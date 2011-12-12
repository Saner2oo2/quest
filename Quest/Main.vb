﻿Imports AxeSoftware.Utility

Public Class Main

    Private m_currentFile As String
    Private m_loaded As Boolean = False
    Private m_playingEditorGame As Boolean = False
    Private m_cmdLineLaunch As String = Nothing
    Private Delegate Sub MenuHandler()

    Public Sub New()
        ' This call is required by the Windows Form Designer.
        InitializeComponent()

        AddHandler System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException, AddressOf CurrentDispatcher_UnhandledException

        ' Add any initialization after the InitializeComponent() call.
        ctlLauncher.QuestVersion = My.Application.Info.Version
        ctlLauncher.DownloadFolder = Options.Instance.GetStringValue(OptionNames.GamesFolder)
        ctlLauncher.ShowSandpit = Options.Instance.GetBooleanValue(OptionNames.ShowSandpit)
        ctlPlayer.Visible = False
        InitialiseMenuHandlers()

        Dim helper As New AxeSoftware.Utility.WindowHelper(Me, "Quest", "Main")

        Dim args As New List(Of String)(Environment.GetCommandLineArgs())
        If args.Count > 1 Then
            CmdLineLaunch(args(1))
        End If

        AddHandler Options.Instance.OptionChanged, AddressOf OptionsChanged
        AddHandler ctlLauncher.BrowseForGame, AddressOf ctlLauncher_BrowseForGame
        AddHandler ctlLauncher.BrowseForGameEdit, AddressOf ctlLauncher_BrowseForGameEdit
        AddHandler ctlLauncher.CreateNewGame, AddressOf ctlLauncher_CreateNewGame
        AddHandler ctlLauncher.EditGame, AddressOf ctlLauncher_EditGame
        AddHandler ctlLauncher.LaunchGame, AddressOf ctlLauncher_LaunchGame
        AddHandler ctlLauncher.Tutorial, AddressOf ctlLauncher_Tutorial
    End Sub

    Private Sub CurrentDispatcher_UnhandledException(sender As Object, e As System.Windows.Threading.DispatcherUnhandledExceptionEventArgs)
        Dim frmError As New ErrorHandler
        frmError.ErrorText = e.Exception.ToString()
        frmError.ShowDialog()
        e.Handled = True
    End Sub

    Private Sub InitialiseMenuHandlers()
        ctlMenu.AddMenuClickHandler("open", AddressOf Browse)
        ctlMenu.AddMenuClickHandler("about", AddressOf AboutMenuClick)
        ctlMenu.AddMenuClickHandler("exit", AddressOf ExitMenuClick)
        ctlMenu.AddMenuClickHandler("openedit", AddressOf OpenEditMenuClick)
        ctlMenu.AddMenuClickHandler("restart", AddressOf RestartMenuClick)
        ctlMenu.AddMenuClickHandler("createnew", AddressOf CreateNewMenuClick)
        ctlMenu.AddMenuClickHandler("viewhelp", AddressOf Help)
        ctlMenu.AddMenuClickHandler("forums", AddressOf Forums)
        ctlMenu.AddMenuClickHandler("logbug", AddressOf LogBug)
        ctlMenu.AddMenuClickHandler("fullscreen", AddressOf GoFullScreen)
        ctlMenu.AddMenuClickHandler("options", AddressOf ShowOptions)
    End Sub

    Private Sub ctlPlayer_AddToRecent(filename As String, name As String) Handles ctlPlayer.AddToRecent
        ctlLauncher.AddToRecent(filename, name)
    End Sub

    Private Sub ctlPlayer_Quit() Handles ctlPlayer.Quit
        FullScreen = False
        If m_playingEditorGame Then
            ctlPlayer.Visible = False
            ctlMenu.Mode = Quest.Controls.Menu.MenuMode.Editor
            ctlEditor.Visible = True
            m_playingEditorGame = False
            ctlEditor.SetWindowTitle()
            ctlEditor.SetMenu(ctlMenu)
            ctlEditor.Redisplay()
        Else
            ctlMenu.Mode = Quest.Controls.Menu.MenuMode.GameBrowser
            ctlLauncher.RefreshLists()
            ctlPlayer.Visible = False
            ctlLauncherHost.Visible = True
            SetWindowTitle()
        End If
    End Sub

    Private Sub ctlLauncher_BrowseForGame()
        Browse()
    End Sub

    Private Sub ctlLauncher_BrowseForGameEdit()
        BrowseEdit()
    End Sub

    Private Sub ctlLauncher_CreateNewGame()
        CreateNewMenuClick()
    End Sub

    Private Sub ctlLauncher_EditGame(filename As String)
        LaunchEdit(filename)
    End Sub

    Private Sub ctlLauncher_LaunchGame(filename As String)
        Launch(filename)
    End Sub

    Private Sub ctlLauncher_Tutorial()
        Tutorial()
    End Sub

    Private Sub Browse()
        Dim startFolder As String = DirectCast(Registry.GetSetting("Quest", "Settings", "StartFolder", _
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), String)

        dlgOpenFile.InitialDirectory = startFolder
        dlgOpenFile.Multiselect = False
        dlgOpenFile.Filter = "Quest Games|*.quest;*.quest-save;*.aslx;*.asl;*.cas;*.qsg|All files|*.*"
        dlgOpenFile.FileName = ""
        dlgOpenFile.ShowDialog()
        If dlgOpenFile.FileName.Length > 0 Then
            Registry.SaveSetting("Quest", "Settings", "StartFolder", System.IO.Path.GetDirectoryName(dlgOpenFile.FileName))

            Launch(dlgOpenFile.FileName)
        End If
    End Sub

    Private Sub Launch(filename As String)
        Dim game As AxeSoftware.Quest.IASL = Nothing

        Try
            m_currentFile = filename
            ctlPlayer.Reset()

            game = GameLauncher.GetGame(filename, String.Empty)

            If game Is Nothing Then
                MsgBox("Unrecognised file type. This game cannot be loaded in Quest.", MsgBoxStyle.Critical)
            Else
                Me.SuspendLayout()
                ctlMenu.Mode = Quest.Controls.Menu.MenuMode.Player
                ctlLauncherHost.Visible = False
                ctlEditor.Visible = False
                ctlPlayer.Visible = True
                ctlPlayer.SetMenu(ctlMenu)
                Me.ResumeLayout()
                'ctlPlayer.RestoreSplitterPositions()
                Application.DoEvents()
                ctlPlayer.UseGameColours = Options.Instance.GetBooleanValue(OptionNames.UseGameColours)
                ctlPlayer.SetPlayerOverrideColours(
                        Options.Instance.GetColourValue(OptionNames.BackgroundColour),
                        Options.Instance.GetColourValue(OptionNames.ForegroundColour),
                        Options.Instance.GetColourValue(OptionNames.LinkColour))
                ctlPlayer.UseGameFont = Options.Instance.GetBooleanValue(OptionNames.UseGameFont)
                ctlPlayer.SetPlayerOverrideFont(
                        Options.Instance.GetStringValue(OptionNames.FontFamily),
                        Options.Instance.GetSingleValue(OptionNames.FontSize),
                        DirectCast(Options.Instance.GetIntValue(OptionNames.FontStyle), FontStyle))
                ctlPlayer.PlaySounds = Options.Instance.GetBooleanValue(OptionNames.PlaySounds)
                ctlPlayer.Initialise(game)
                ctlPlayer.Focus()
            End If

        Catch ex As Exception
            MsgBox("Error launching game: " & ex.Message)
        End Try

    End Sub

    Private Sub LaunchEdit(filename As String)
        Dim game As AxeSoftware.Quest.IASL = Nothing
        Dim ext As String

        Try
            Me.Cursor = Cursors.WaitCursor
            ext = System.IO.Path.GetExtension(filename)

            Select Case ext
                Case ".aslx"
                    Me.SuspendLayout()
                    ctlMenu.Mode = Quest.Controls.Menu.MenuMode.Editor
                    ctlLauncherHost.Visible = False
                    ctlPlayer.Visible = False
                    ctlEditor.Visible = True
                    ctlEditor.SetMenu(ctlMenu)
                    Me.ResumeLayout()
                    'ctlPlayer.RestoreSplitterPositions()
                    Application.DoEvents()
                    ctlEditor.Initialise(filename)
                    ctlEditor.Focus()
                Case Else
                    MsgBox(String.Format("Unrecognised file type '{0}'", ext))
            End Select

        Catch ex As Exception
            MsgBox("Error loading game: " + Environment.NewLine + Environment.NewLine + ex.Message, MsgBoxStyle.Critical)
            Me.Cursor = Cursors.Default
        End Try

    End Sub

    Private Sub ctlEditor_InitialiseFinished(success As Boolean) Handles ctlEditor.InitialiseFinished
        Me.Cursor = Cursors.Default
        ctlMenu.Visible = True
        If Not success Then
            CloseEditor()
        End If
    End Sub

    Private Sub AboutMenuClick()
        Dim frmAbout As New About
        frmAbout.ShowDialog()
    End Sub

    Private Sub ctlPlayer_GameNameSet(name As String) Handles ctlPlayer.GameNameSet
        SetWindowTitle(name)
    End Sub

    Private Sub SetWindowTitle(Optional gameName As String = "")
        Dim caption As String
        caption = "Quest"
        If Not String.IsNullOrEmpty(gameName) Then caption += " - " + gameName
        Me.Text = caption
    End Sub

    Private Sub ExitMenuClick()
        Me.Close()
    End Sub

    Private Sub RestartMenuClick()
        Launch(m_currentFile)
    End Sub

    Private Sub OpenEditMenuClick()
        If Not ctlEditor.CheckGameIsSaved("Do you wish to save your changes before opening a new game?") Then Return
        BrowseEdit()
    End Sub

    Private Sub CreateNewMenuClick()
        If Not ctlEditor.CheckGameIsSaved("Do you wish to save your changes before creating a new game?") Then Return

        Dim newFile = ctlEditor.CreateNewGame()
        If String.IsNullOrEmpty(newFile) Then Return

        LaunchEdit(newFile)
    End Sub

    Private Sub BrowseEdit()
        Dim startFolder As String = DirectCast(Registry.GetSetting("Quest", "Settings", "StartFolder", _
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), String)

        dlgOpenFile.InitialDirectory = startFolder
        dlgOpenFile.Multiselect = False
        dlgOpenFile.Filter = "Quest Games (*.aslx)|*.aslx|All files|*.*"
        dlgOpenFile.FileName = ""
        dlgOpenFile.ShowDialog()
        If dlgOpenFile.FileName.Length > 0 Then
            Registry.SaveSetting("Quest", "Settings", "StartFolder", System.IO.Path.GetDirectoryName(dlgOpenFile.FileName))

            LaunchEdit(dlgOpenFile.FileName)
        End If

    End Sub

    Private Sub ctlEditor_AddToRecent(filename As String, name As String) Handles ctlEditor.AddToRecent
        ctlLauncher.AddToEditorRecent(filename, name)
    End Sub

    Private Sub Main_FormClosing(sender As Object, e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        ctlPlayer.WindowClosing()
        If Not ctlEditor.CloseEditor(False, True) Then
            e.Cancel = True
        End If
    End Sub

    Private Sub Main_KeyDown(sender As Object, e As System.Windows.Forms.KeyEventArgs) Handles Me.KeyDown
        'If e.KeyCode = Keys.F11 Then
        '    Dim frmError As New ErrorHandler
        '    frmError.ErrorText = "Test error"
        '    frmError.ShowDialog()
        'End If

        If e.KeyCode = Keys.Escape AndAlso FullScreen Then
            FullScreen = False
        End If
        If e.KeyCode = Keys.Enter Then
            ctlPlayer.KeyPressed()
        End If
    End Sub

    Private Sub Main_KeyPress(sender As Object, e As System.Windows.Forms.KeyPressEventArgs) Handles Me.KeyPress
        e.Handled = ctlPlayer.KeyPressed()
    End Sub

    Private Sub ctlEditor_Close() Handles ctlEditor.Close
        CloseEditor()
    End Sub

    Private Sub CloseEditor()
        ctlMenu.Mode = Quest.Controls.Menu.MenuMode.GameBrowser
        ctlLauncher.RefreshLists()
        ctlEditor.Visible = False
        ctlEditor.CancelUnsavedChanges()
        ctlLauncherHost.Visible = True
        SetWindowTitle()
    End Sub

    Private Sub ctlEditor_Loaded(name As String) Handles ctlEditor.Loaded
        SetWindowTitle(name)
    End Sub

    Private Sub ctlEditor_NewGame() Handles ctlEditor.NewGame
        CreateNewMenuClick()
    End Sub

    Private Sub ctlEditor_OpenGame() Handles ctlEditor.OpenGame
        OpenEditMenuClick()
    End Sub

    Private Sub ctlEditor_Play(filename As String) Handles ctlEditor.Play
        m_playingEditorGame = True
        Launch(filename)
    End Sub

    Private Sub ctlEditor_PlayWalkthrough(filename As String, walkthrough As String, record As Boolean) Handles ctlEditor.PlayWalkthrough
        m_playingEditorGame = True
        ctlPlayer.PreLaunchAction = Sub() ctlPlayer.InitWalthrough(walkthrough)
        ctlPlayer.PostLaunchAction = Sub() ctlPlayer.StartWalkthrough()
        If record Then
            ctlPlayer.RecordWalkthrough = walkthrough
        End If
        Launch(filename)
    End Sub

    Private Sub Main_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        ctlLauncher.MainWindowShown()

        If m_cmdLineLaunch IsNot Nothing Then
            Launch(m_cmdLineLaunch)
        End If
    End Sub

    Private Sub ctlPlayer_ShortcutKeyPressed(keys As System.Windows.Forms.Keys) Handles ctlPlayer.ShortcutKeyPressed
        ctlMenu.ShortcutKeyPressed(keys)
    End Sub

    Private Sub CmdLineLaunch(filename As String)
        ctlLauncherHost.Visible = False
        m_cmdLineLaunch = filename
    End Sub

    Private Sub LogBug()
        LaunchURL("http://quest.codeplex.com/workitem/list/basic")
    End Sub

    Private Sub Forums()
        LaunchURL("http://www.textadventures.co.uk/forum/")
    End Sub

    Private Sub Help()
        LaunchURL("http://quest5.net")
    End Sub

    Private Sub Tutorial()
        LaunchURL("http://quest5.net/wiki/Tutorial")
    End Sub

    Private Sub LaunchURL(url As String)
        Try
            System.Diagnostics.Process.Start(url)
        Catch ex As Exception
            MsgBox(String.Format("Error launching {0}{1}{2}", url, Environment.NewLine + Environment.NewLine, ex.Message), MsgBoxStyle.Critical, "Quest")
        End Try
    End Sub

    Private Sub GoFullScreen()
        FullScreen = True
        ctlPlayer.ShowExitFullScreenButton()
    End Sub

    Private m_fullScreen As Boolean

    Public Property FullScreen As Boolean
        Get
            Return m_fullScreen
        End Get
        Set(value As Boolean)
            If m_fullScreen <> value Then
                m_fullScreen = value
                Me.FormBorderStyle = If(m_fullScreen, Windows.Forms.FormBorderStyle.None, Windows.Forms.FormBorderStyle.Sizable)
                Me.WindowState = If(m_fullScreen, FormWindowState.Maximized, FormWindowState.Normal)
                ctlMenu.Visible = Not m_fullScreen
            End If
        End Set
    End Property

    Private Sub ctlPlayer_ExitFullScreen() Handles ctlPlayer.ExitFullScreen
        FullScreen = False
    End Sub

    Private Sub ctlPlayer_RecordedWalkthrough(name As String, steps As System.Collections.Generic.List(Of String)) Handles ctlPlayer.RecordedWalkthrough
        ctlEditor.SetRecordedWalkthrough(name, steps)
    End Sub

    Private Sub ShowOptions()
        Dim optionsForm As New OptionsDialog
        optionsForm.ShowDialog()
    End Sub

    Private Sub OptionsChanged(optionName As OptionNames)
        Select Case optionName
            Case OptionNames.BackgroundColour, OptionNames.ForegroundColour, OptionNames.LinkColour
                ctlPlayer.SetPlayerOverrideColours(
                    Options.Instance.GetColourValue(OptionNames.BackgroundColour),
                    Options.Instance.GetColourValue(OptionNames.ForegroundColour),
                    Options.Instance.GetColourValue(OptionNames.LinkColour))
            Case OptionNames.UseGameColours
                ctlPlayer.UseGameColours = Options.Instance.GetBooleanValue(OptionNames.UseGameColours)
            Case OptionNames.FontFamily, OptionNames.FontSize, OptionNames.FontStyle
                ctlPlayer.SetPlayerOverrideFont(
                    Options.Instance.GetStringValue(OptionNames.FontFamily),
                    Options.Instance.GetSingleValue(OptionNames.FontSize),
                    DirectCast(Options.Instance.GetIntValue(OptionNames.FontStyle), FontStyle))
            Case OptionNames.UseGameFont
                ctlPlayer.UseGameFont = Options.Instance.GetBooleanValue(OptionNames.UseGameFont)
            Case OptionNames.GamesFolder
                ctlLauncher.DownloadFolder = Options.Instance.GetStringValue(OptionNames.GamesFolder)
            Case OptionNames.ShowSandpit
                ctlLauncher.ShowSandpit = Options.Instance.GetBooleanValue(OptionNames.ShowSandpit)
            Case OptionNames.PlaySounds
                ctlPlayer.PlaySounds = Options.Instance.GetBooleanValue(OptionNames.PlaySounds)
        End Select
    End Sub

End Class
