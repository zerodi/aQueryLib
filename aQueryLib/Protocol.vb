Option Strict On

#Region "Using directives"
Imports System
Imports System.Net
Imports System.Collections.Specialized
Imports System.Net.Sockets
#End Region

Namespace aQueryLib
    Friend MustInherit Class Protocol
        ' This integer constant represents an integer with the top bit set. It is needed when
        ' Checking of a Source server is using BZip2 compression
        Private Const _INT_TOPBIT As Integer = -2147483648

        Private _serverConnection As Socket
        Private _remoteIpEndPoint As IPEndPoint
        Private _sendBuffer As Byte(), _readBuffer As Byte()
        Private _timeout As Integer = 5000, _offset As Integer
        Private _scanTime As DateTime
        Private _debugMode As Boolean

        Protected _requestString As String = "", _responseString As String = ""
        Protected _isOnline As Boolean = True
        Protected _packages As Integer
        Protected _protocol As GameProtocol
        Protected _players As PlayerCollection
        Protected _params As StringDictionary
        Protected _teams As StringCollection

        Public Sub New()
            _players = New PlayerCollection()
            _params = New StringDictionary()
            _teams = New StringCollection()
        End Sub

#Region "Protected members"
        Protected Sub Connect(ByVal host As String, ByVal port As Integer)
            _serverConnection = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            _serverConnection.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, _timeout)

            Dim ip As IPAddress
            Try
                ip = IPAddress.Parse(host)
            Catch generatedExceptionName As System.FormatException
                ip = Dns.GetHostEntry(host).AddressList(0)
            End Try
            _remoteIpEndPoint = New IPEndPoint(ip, port)
        End Sub

        Protected Function Query(ByVal request As String) As Boolean
            _readBuffer = New Byte(100 * 1024 - 1) {}
            ' 100kb should be enough
            Dim _remoteEndPoint As EndPoint = DirectCast(_remoteIpEndPoint, EndPoint)
            _packages = 0
            Dim read As Integer = 0, bufferOffset As Integer = 0

            ' Request
            _sendBuffer = System.Text.Encoding.[Default].GetBytes(request)

            _serverConnection.SendTo(_sendBuffer, _remoteIpEndPoint)

            ' Response
            Do
                read = 0
                Try
                    ' Multipackage check
                    If _packages > 0 Then
                        Select Case _protocol
                            Case GameProtocol.Source, GameProtocol.HalfLife
                                ' This is a multi-package response only if the initial response includes a special header,
                                ' starting with the integer -2.
                                If BitConverter.ToInt32(_readBuffer, 0) = -2 Then
                                    ' We need to add all packets to a sorted list, sorted by their index.
                                    ' This is because we can never be certain what order the packets arrive in.
                                    Dim packets As New SortedList(Of Integer, Byte())
                                    Dim packetData As New List(Of Byte)

                                    ' There are some values in this special "split-packet" header that we'll need to hold onto before moving along.
                                    Dim requestID As Integer = BitConverter.ToInt32(_readBuffer, 4)
                                    Dim usesBzip2 As Boolean = (_INT_TOPBIT And requestID) = _INT_TOPBIT ' Determine if the top bit is set in the request ID, which denotes that BZip2 compression is used.
                                    Dim numPackets As Byte = _readBuffer(8)
                                    Dim splitSize As Integer
                                    Dim crcChecksum As Integer
                                    Dim headerSize As Integer


                                    ' The source protocol includes an extra byte here that'll always contain 0,
                                    ' which we'd want to ignore, but only if the protocol in use is Source.
                                    If _protocol = GameProtocol.Source Then
                                        headerSize = 18
                                        splitSize = BitConverter.ToInt32(_readBuffer, 10)
                                        If usesBzip2 Then
                                            crcChecksum = BitConverter.ToInt32(_readBuffer, 14)
                                        End If
                                    Else
                                        headerSize = 9
                                        If usesBzip2 Then
                                            crcChecksum = BitConverter.ToInt32(_readBuffer, 11)
                                            headerSize += 2
                                        End If
                                    End If

                                    ' Add the first packet (that was sent along with the special "split-packet" header) to the list of bytes.
                                    For j As Integer = headerSize To bufferOffset - 1
                                        packetData.Add(_readBuffer(j))
                                    Next

                                    ' Now, add this to the sorted list, and pass the packet number as the key.
                                    ' The packet index is placed differently on GoldSource servers than on
                                    ' Source server, hence this IF-statement.
                                    If _protocol = GameProtocol.Source Then
                                        packets.Add(_readBuffer(9), packetData.ToArray())
                                    Else
                                        packets.Add(_readBuffer(8) >> 4, packetData.ToArray()) ' The upper 4 bits represent the packet index.
                                    End If

                                    packetData.Clear()

                                    ' Read the next packets.
                                    For i As Integer = 0 To numPackets - 2
                                        read = _serverConnection.ReceiveFrom(_readBuffer, _remoteEndPoint)
                                        For j As Integer = headerSize - 8 To read ' Subtract headerSize by 4 here because the 4 bytes used for CRC is only included in the first header.
                                            packetData.Add(_readBuffer(j))
                                        Next
                                        If _protocol = GameProtocol.Source Then
                                            packets.Add(_readBuffer(9), packetData.ToArray())
                                        Else
                                            packets.Add(_readBuffer(8) >> 4, packetData.ToArray()) ' The upper 4 bits represent the packet index.
                                        End If
                                        packetData.Clear()
                                    Next

                                    ' Once we have gotten this far, the SortedList "packet", should
                                    ' Contain all packets in sorted order, now we just need to append them in that order.
                                    For i As Integer = 0 To packets.Count - 1
                                        packetData.AddRange(packets.Values(i))
                                    Next

                                    If usesBzip2 Then

                                        Dim inStream As New System.IO.MemoryStream(packetData.ToArray(), 0, packetData.Count)
                                        Dim outStream As New System.IO.MemoryStream()
                                        Dim crcStream As System.IO.MemoryStream
                                        Dim crc As New CRC32()

                                        ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress(inStream, outStream, True)

                                        bufferOffset = splitSize
                                        crcStream = New System.IO.MemoryStream(outStream.GetBuffer(), 0, bufferOffset, True, True)
                                        'outStream.Capacity = bufferOffset
                                        'outStream.Position = 0

                                        _readBuffer = crcStream.GetBuffer()

                                        read = 0
                                        If crcChecksum <> crc.GetCrc32(crcStream) Then
                                            Return False
                                        End If

                                        inStream.Close()
                                        outStream.Close()
                                    Else
                                        _readBuffer = packetData.ToArray()
                                        bufferOffset = _readBuffer.Length
                                        read = 0
                                    End If
                                End If
                        End Select
                    Else
                        ' first package
                        read = _serverConnection.ReceiveFrom(_readBuffer, _remoteEndPoint)
                    End If
                    bufferOffset += read
                    _packages += 1
                Catch generatedExceptionName As System.Net.Sockets.SocketException
                    _isOnline = False
                    Return False
                End Try
            Loop While read > 0

            _scanTime = DateTime.Now

            If bufferOffset > 0 AndAlso bufferOffset <> _readBuffer.Length Then
                Dim temp As Byte() = New Byte(bufferOffset - 1) {}
                For i As Integer = 0 To temp.Length - 1
                    temp(i) = _readBuffer(i)
                Next
                _readBuffer = temp
                temp = Nothing
            End If
            _responseString = System.Text.Encoding.[Default].GetString(_readBuffer)

            If _debugMode Then
                Dim stream As System.IO.FileStream = System.IO.File.OpenWrite("LastQuery.dat")
                stream.Write(_readBuffer, 0, _readBuffer.Length)
                stream.Close()
            End If
            Return True
        End Function

        Protected Sub AddParams(ByVal parts As String())
            If Not IsOnline Then
                Exit Sub
            End If
            Dim key As String, val As String
            For i As Integer = 0 To parts.Length - 1
                If parts(i).Length = 0 Then
                    Continue For
                End If
                key = parts(System.Math.Max(System.Threading.Interlocked.Increment(i), i - 1))
                val = parts(i)

                If key = "final" Then
                    Exit For
                End If
                If key = "querid" Then
                    Continue For
                End If

                _params(key) = val
            Next
        End Sub

        Protected ReadOnly Property Response() As Byte()
            Get
                Return _readBuffer
            End Get
        End Property

        Protected ReadOnly Property ResponseString() As String
            Get
                Return _responseString
            End Get
        End Property

        Protected Property Offset() As Integer
            Get
                Return _offset
            End Get
            Set(ByVal value As Integer)
                _offset = value
            End Set
        End Property

        Protected Function ReadNextParam(ByVal offset As Integer) As String
            If offset > _readBuffer.Length Then
                Throw New IndexOutOfRangeException()
            End If
            _offset = offset
            Return ReadNextParam()
        End Function

        Protected Function ReadNextParam() As String
            Dim temp As String = ""
            While _offset < _readBuffer.Length
                If _readBuffer(_offset) = 0 Then
                    _offset += 1
                    Exit While
                End If
                temp += ChrW(_readBuffer(_offset))
                _offset += 1
            End While
            Return temp
        End Function
#End Region

#Region "Properties"
        ''' <summary>
        ''' Gets or sets the connection timeout
        ''' </summary>
        Public Property Timeout() As Integer
            Get
                Return _timeout
            End Get
            Set(ByVal value As Integer)
                _timeout = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the parsed parameters
        ''' </summary>
        Public ReadOnly Property Parameters() As StringDictionary
            Get
                Return _params
            End Get
        End Property

        ''' <summary>
        ''' Gets the team names, not always set
        ''' </summary>
        Public ReadOnly Property Teams() As StringCollection
            Get
                Return _teams
            End Get
        End Property

        ''' <summary>
        ''' Gets the players on the server
        ''' </summary>
        Public ReadOnly Property Players() As PlayerCollection
            Get
                Return _players
            End Get
        End Property

        ''' <summary>
        ''' Gets the number of players on the server
        ''' </summary>
        Public ReadOnly Property NumPlayers() As Integer
            Get
                Return _players.Count
            End Get
        End Property

        ''' <summary>
        ''' Gets the time of the last scan
        ''' </summary>
        Public ReadOnly Property ScanTime() As DateTime
            Get
                Return _scanTime
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
                _debugMode = value
            End Set
        End Property
#End Region

#Region "Abstract and virtual members"
        ''' <summary>
        ''' Querys the server info
        ''' </summary>
        Public MustOverride Sub GetServerInfo()

        ''' <summary>
        ''' Gets the server name
        ''' </summary>
        Public Overridable ReadOnly Property Name() As String
            Get
                If Not _isOnline Then
                    Return Nothing
                End If
                Return _params("hostname")
            End Get
        End Property

        ''' <summary>
        ''' Determines the mod
        ''' </summary>
        Public Overridable ReadOnly Property [Mod]() As String
            Get
                If Not _isOnline Then
                    Return Nothing
                End If
                Return _params("modname")
            End Get
        End Property

        ''' <summary>
        ''' Determines the mapname
        ''' </summary>
        Public Overridable ReadOnly Property Map() As String
            Get
                If Not _isOnline Then
                    Return Nothing
                End If
                Return _params("mapname")
            End Get
        End Property

        ''' <summary>
        ''' Determines if the server is password protected
        ''' </summary>
        Public Overridable ReadOnly Property Passworded() As Boolean
            Get
                If _params.ContainsKey("passworded") AndAlso (_params("passworded") <> "0") Then
                    Return True
                End If
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Determines if the server is online
        ''' </summary>
        Public Overridable ReadOnly Property IsOnline() As Boolean
            Get
                Return _isOnline
            End Get
        End Property

        ''' <summary>
        ''' Gets the max players
        ''' </summary>
        Public Overridable ReadOnly Property MaxPlayers() As Integer
            Get
                Return Int16.Parse(_params("maxplayers"))
            End Get
        End Property
#End Region
    End Class
End Namespace
