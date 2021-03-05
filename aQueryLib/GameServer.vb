#Region "Using directives"
Imports System
#End Region

Namespace aQueryLib
    ''' <summary>
    ''' A class to query gaming servers
    ''' </summary>
    Public Class GameServer
        Private _host As String
        Private _port As Integer, _timeOut As Integer = 1500
        Private _type As GameType
        Private _serverInfo As Protocol
        Private _debugMode As Boolean = False

        ''' <param name="host">The IP address or the hostname of the gameserver</param>
        ''' <param name="port">The port of the gameserver</param>
        ''' <param name="type">The gameserver type</param>
        Public Sub New(ByVal host As String, ByVal port As Integer, ByVal type As GameType)
            _host = host
            _port = port
            _type = type

            CheckServerType()
        End Sub

        ''' <param name="host">The IP address or the hostname of the gameserver</param>
        ''' <param name="port">The port of the gameserver</param>
        ''' <param name="type">The gameserver type</param>
        ''' <param name="timeout">The timeout for the query</param>
        Public Sub New(ByVal host As String, ByVal port As Integer, ByVal type As GameType, ByVal timeout As Integer)
            _host = host
            _port = port
            _type = type
            _timeOut = timeout

            CheckServerType()
        End Sub

        Private Sub CheckServerType()
            Select Case DirectCast(_type, GameProtocol)

                Case GameProtocol.HalfLife
                    _serverInfo = New HalfLife(_host, _port)
                    Exit Select

                Case GameProtocol.Source
                    _serverInfo = New Source(_host, _port)
                    Exit Select

                Case Else ' GameProtocol.None
                    Throw New System.NotImplementedException()

            End Select
            _serverInfo.DebugMode = _debugMode
        End Sub

        ''' <summary>
        ''' Querys the serverinfos
        ''' </summary>
        Public Sub QueryServer()
            _serverInfo.GetServerInfo()
        End Sub

        ''' <summary>
        ''' Cleans the color codes from the player names
        ''' </summary>
        ''' <param name="name">Playername</param>
        ''' <returns>Cleaned playername</returns>
        Public Shared Function CleanName(ByVal name As String) As String
            Dim regex As New System.Text.RegularExpressions.Regex("(\^\d)|(\$\d)")
            Return regex.Replace(name, "")
        End Function

#Region "Properties"
        ''' <summary>
        ''' Gets or sets the connectiontimeout
        ''' </summary>
        Public Property Timeout() As Integer
            Get
                Return _serverInfo.Timeout
            End Get
            Set(ByVal value As Integer)
                _serverInfo.Timeout = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the parsed parameters
        ''' </summary>
        Public ReadOnly Property Parameters() As System.Collections.Specialized.StringDictionary
            Get
                Return _serverInfo.Parameters
            End Get
        End Property

        ''' <summary>
        ''' Gets if the server is online
        ''' </summary>
        Public ReadOnly Property IsOnline() As Boolean
            Get
                Return _serverInfo.IsOnline
            End Get
        End Property

        ''' <summary>
        ''' Gets the time the last scan
        ''' </summary>
        Public ReadOnly Property ScanTime() As DateTime
            Get
                Return _serverInfo.ScanTime
            End Get
        End Property

        ''' <summary>
        ''' Gets the players on the server
        ''' </summary>
        Public ReadOnly Property Players() As PlayerCollection
            Get
                Return _serverInfo.Players
            End Get
        End Property

        ''' <summary>
        ''' Get the teamnames if there are any
        ''' </summary>
        Public ReadOnly Property Teams() As System.Collections.Specialized.StringCollection
            Get
                Return _serverInfo.Teams
            End Get
        End Property

        ''' <summary>
        ''' Gets the maximal player number
        ''' </summary>
        Public ReadOnly Property MaxPlayers() As Integer
            Get
                Return _serverInfo.MaxPlayers
            End Get
        End Property

        ''' <summary>
        ''' Gets the number of players on the server
        ''' </summary>
        Public ReadOnly Property NumPlayers() As Integer
            Get
                Return _serverInfo.NumPlayers
            End Get
        End Property

        ''' <summary>
        ''' Gets the servername
        ''' </summary>
        Public ReadOnly Property Name() As String
            Get
                Return _serverInfo.Name
            End Get
        End Property

        ''' <summary>
        ''' Gets the active modification
        ''' </summary>
        Public ReadOnly Property [Mod]() As String
            Get
                Return _serverInfo.[Mod]
            End Get
        End Property

        ''' <summary>
        ''' Gets the mapname
        ''' </summary>
        Public ReadOnly Property Map() As String
            Get
                Return _serverInfo.Map
            End Get
        End Property

        ''' <summary>
        ''' Gets if the server is password protected
        ''' </summary>
        Public ReadOnly Property Passworded() As Boolean
            Get
                Return _serverInfo.Passworded
            End Get
        End Property

        ''' <summary>
        ''' Gets the server gametype
        ''' </summary>
        Public ReadOnly Property GameType() As GameType
            Get
                Return _type
            End Get
        End Property

        ''' <summary>
        ''' Gets the used protocol
        ''' </summary>
        ''' <value></value>
        Public ReadOnly Property Protocol() As GameProtocol
            Get
                Return DirectCast(_type, GameProtocol)
            End Get
        End Property

        ''' <summary>
        ''' Enables the debugging mode
        ''' </summary>
        Public Property DebugMode() As Boolean
            Get
                Return _debugMode
            End Get
            Set(ByVal value As Boolean)
                If _serverInfo IsNot Nothing Then
                    _serverInfo.DebugMode = value
                End If
                _debugMode = value
            End Set
        End Property
#End Region
    End Class
End Namespace