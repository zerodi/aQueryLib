Option Strict On

Imports System.Net.Sockets
Namespace aQueryLib
    Public Class SourceMasterServer
        ' When binding the query socket to a port, 51500 is the lowest port number
        ' we'll use.
        Private Const PORT_LOWER_BOUND As Integer = 51500
        ' A datagram can not be larger than 1400 bytes.
        Private Const MAX_DATAGRAM_SIZE As Integer = 1400
        ' The timeout that we will use in our Receive calls.
        Private Const TIMEOUT As Integer = 1600

        ' This callback is used whenever we perform an async master server query
        ' with subsequent queries to each game server.
        Private asyncCallBack As QueryCallBack

        ' The main thread used for querying the master server.
        Private queryThread As System.Threading.Thread

        ' A boolean flag used to denote that cancelation is pending.
        Private queryCancellationPending As Boolean

        ' This class is used to enqueue the queries.
        Private queryPool As QueryThreadPool

        ' Event raised when an async query has completed.
        Public Event QueryAsyncCompleted(ByVal sender As Object, ByVal e As System.EventArgs)

        ' This ISynchronizeInvoke object will let us marshal callback calls to the
        ' Proper thread.
        Private syncObject As System.ComponentModel.ISynchronizeInvoke = Nothing

        Public Delegate Sub QueryRawCallBack(ByVal sender As Object, ByVal e As QueryRawAsyncEventArgs)
        Public Delegate Sub QueryCallBack(ByVal sender As Object, ByVal e As QueryAsyncEventArgs)

        Public Sub New()
            Me.queryPool = New QueryThreadPool(20)
            AddHandler Me.queryPool.AllQueriesProcessed, AddressOf Me.QueryPool_AllQueriesProcessed
        End Sub

        Private Sub QueryPool_AllQueriesProcessed(ByVal sender As Object, ByVal e As EventArgs)
            Me.OnQueryAsyncCompleted()
        End Sub

        ''' <summary>
        ''' Queries the master server and returns the raw data as an array of IPEndPoint.
        ''' </summary>
        ''' <param name="game">What type of servers to query for.</param>
        ''' <param name="region">What region to filter on.</param>
        ''' <param name="filter">Specifies a filter for the query. Several filters can be set using bitwise OR.</param>
        ''' <param name="map">A map to filter on. Pass String.Empty for all maps.</param>
        ''' <param name="gameMod">A mod to filter on. Pass String.Empty for all mods.</param>
        ''' <returns>Returns an array of IPEndPoint</returns>
        ''' <remarks></remarks>
        Public Function QueryRaw(ByVal game As QueryGame, ByVal region As QueryRegionCode, ByVal filter As QueryFilter, ByVal map As String, ByVal gameMod As String) As System.Net.IPEndPoint()
            Dim received, retries, maxRetries As Integer
            Dim endOfQuery As Boolean = False
            Dim timeoutOccured As Boolean = False
            Dim masterEndPoint As System.Net.EndPoint = Me.GetMasterServerEndPoint(game)
            Dim gameServers As New List(Of System.Net.IPEndPoint)()
            Dim udpSocket As New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            Dim query As Byte() = Me.BuildQuery(region, filter, map, gameMod, "0.0.0.0:0")

            Dim buffer(MAX_DATAGRAM_SIZE - 1) As Byte

            ' Initially, we will try to re-send the query 10 times
            ' if we arent getting any reply.
            maxRetries = 10

            udpSocket.Bind(New System.Net.IPEndPoint(System.Net.IPAddress.Any, 51500))
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, SourceMasterServer.TIMEOUT)
            ' Send the query
            Do ' Keep querying until the end of the IP list.
                retries = 0
                Do ' Keep sending the query if there is no reply.
                    retries += 1
                    udpSocket.SendTo(query, masterEndPoint)
                    Try
                        received = udpSocket.ReceiveFrom(buffer, masterEndPoint)
                        timeoutOccured = False
                    Catch ex As Exception
                        ' Timeout occured
                        timeoutOccured = True
                    End Try

                Loop While timeoutOccured And retries < maxRetries

                ' Once we have gotten this far, we KNOW that the server is online,
                ' so lets increate the maxRetries.
                maxRetries = 20
                ' If timeoutOccured is True at this point, we know that we where unable
                ' to get a reply and had to give up.
                If timeoutOccured Then
                    Throw New NoReplyException("No response from the master server.")
                End If

                ' Add the newly received IPs to the list.
                gameServers.AddRange(Me.ParseReplyBuffer(buffer, received))

                If Me.IsNullAddress(gameServers(gameServers.Count - 1)) Then
                    ' We have reached the end of the IP list.
                    endOfQuery = True
                Else
                    ' We have not yet reached the end of the IP list.
                    ' We need to re-query the server to receive more of the list.
                    query = Me.BuildQuery(region, filter, map, gameMod, gameServers(gameServers.Count - 1).ToString())
                End If
            Loop While Not endOfQuery

            Return gameServers.ToArray()
        End Function

        ''' <summary>
        ''' Queries the master server asyncronously and invokes a callback
        ''' each time a portion of the IP list has been received. Returns the servers in raw format,
        ''' that is, as an array of IPEndPoint.
        ''' </summary>
        ''' <param name="game">What type of servers to query for.</param>
        ''' <param name="region">What region to filter on</param>
        ''' <param name="filter">Specifies a filter for the query. Several filters can be set using bitwise OR.</param>
        ''' <param name="map">A map to filter on. Pass String.Empty for all maps.</param>
        ''' <param name="gameMod">A mod to filter on. Pass String.Empty for all mods.</param>
        ''' <param name="callback">The callback to be invoked each time a portion of the IP list has been received.</param>
        ''' <remarks></remarks>
        Public Sub QueryRawAsync(ByVal game As QueryGame, ByVal region As QueryRegionCode, ByVal filter As QueryFilter, ByVal map As String, ByVal gameMod As String, ByVal callback As QueryRawCallBack)
            If Not Me.queryThread Is Nothing AndAlso Me.queryThread.IsAlive Then
                Throw New InvalidOperationException("Query is already running. You can either cancel the query by calling CancelAsyncQuery, or wait for the query to finish")
            End If
            Me.queryCancellationPending = False
            Me.queryThread = New System.Threading.Thread(AddressOf Me.RunAsyncQuery)
            Me.queryThread.IsBackground = True
            Me.queryThread.Start(New Object() {game, region, filter, map, gameMod, callback, True})
        End Sub

        ''' <summary>
        ''' Queries the master server asyncronously and subsequently
        ''' queries the returned game server.
        ''' </summary>
        ''' <param name="game">What type of servers to query for.</param>
        ''' <param name="region">What region to filter on</param>
        ''' <param name="filter">Specifies a filter for the query. Several filters can be set using bitwise OR.</param>
        ''' <param name="map">A map to filter on. Pass String.Empty for all maps.</param>
        ''' <param name="gameMod">A mod to filter on. Pass String.Empty for all mods.</param>
        ''' <param name="callback">The callback to be invoked each time a gameserver has been queried.</param>
        ''' <remarks></remarks>
        Public Sub QueryAsync(ByVal game As QueryGame, ByVal region As QueryRegionCode, ByVal filter As QueryFilter, ByVal map As String, ByVal gameMod As String, ByVal callback As QueryCallBack)
            If Not Me.queryThread Is Nothing AndAlso Me.queryThread.IsAlive Then
                Throw New InvalidOperationException("Query is already running. You can either cancel the query by calling CancelAsyncQuery, or wait for the query to finish")
            End If
            Me.queryCancellationPending = False
            Me.asyncCallBack = callback
            Me.queryThread = New System.Threading.Thread(AddressOf Me.RunAsyncQuery)
            Me.queryThread.IsBackground = True
            Me.queryThread.Start(New Object() {game, region, filter, map, gameMod, New QueryRawCallBack(AddressOf Me.QueryAsync_CallBack), False})
        End Sub

        Private Sub RunAsyncQuery(ByVal parameters As Object)
            ' The parameters parameter will hold an Object array containing all the 
            ' data needed to perform a query.
            Dim arguments() As Object = DirectCast(parameters, Object())
            Dim game As QueryGame = DirectCast(arguments(0), QueryGame)
            Dim region As QueryRegionCode = DirectCast(arguments(1), QueryRegionCode)
            Dim filter As QueryFilter = DirectCast(arguments(2), QueryFilter)
            Dim map As String = DirectCast(arguments(3), String)
            Dim gameMod As String = DirectCast(arguments(4), String)
            Dim callBack As QueryRawCallBack = DirectCast(arguments(5), QueryRawCallBack)
            Dim returnRaw As Boolean = DirectCast(arguments(6), Boolean)

            Dim received, retries, maxRetries, totalServers As Integer
            Dim endOfQuery As Boolean = False
            Dim timeoutOccured As Boolean = False
            Dim masterEndPoint As System.Net.EndPoint = Me.GetMasterServerEndPoint(game)
            Dim gameServers As List(Of System.Net.IPEndPoint)
            Dim udpSocket As New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            Dim query As Byte() = Me.BuildQuery(region, filter, map, gameMod, "0.0.0.0:0")

            Dim buffer(MAX_DATAGRAM_SIZE - 1) As Byte

            ' Initially, we will try to re-send the query 10 times
            ' if we arent getting any reply.
            maxRetries = 10

            ' Keep track of how many servers we have queued for querying.
            ' If this is 0 at the end of this method, we will know that no
            ' IPs where returned.
            totalServers = 0

            Me.BindSocket(udpSocket)
            'udpSocket.Bind(New System.Net.IPEndPoint(System.Net.IPAddress.Any, 51500))
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, SourceMasterServer.TIMEOUT)
            ' Send the query
            Do ' Keep querying until the end of the IP list.
                retries = 0
                Do ' Keep sending the query if there is no reply.
                    retries += 1
                    udpSocket.SendTo(query, masterEndPoint)
                    Try
                        received = udpSocket.ReceiveFrom(buffer, masterEndPoint)
                        timeoutOccured = False
                    Catch ex As Exception
                        ' Timeout occured
                        timeoutOccured = True
                    End Try

                Loop While Not Me.queryCancellationPending AndAlso timeoutOccured AndAlso retries < maxRetries

                If Not Me.queryCancellationPending Then
                    ' Once we have gotten this far, we KNOW that the server is online,
                    ' so lets increate the maxRetries.
                    maxRetries = 20
                    ' If timeoutOccured is True at this point, we know that we where unable
                    ' to get a reply and had to give up.

                    If Not timeoutOccured Then
                        ' Parse the reply.
                        gameServers = Me.ParseReplyBuffer(buffer, received)

                        If Me.IsNullAddress(gameServers(gameServers.Count - 1)) Then
                            ' We have reached the end of the IP list.

                            ' Remove the dummy address that denotes the end of the list.
                            gameServers.RemoveAt(gameServers.Count - 1)
                            endOfQuery = True
                        Else
                            ' We have not yet reached the end of the IP list.
                            ' We need to re-query the server to receive more of the list.
                            query = Me.BuildQuery(region, filter, map, gameMod, gameServers(gameServers.Count - 1).ToString())
                        End If

                        ' Keep track of how many servers we've enqueued for querying.
                        totalServers += gameServers.Count

                        ' Invoke the callback to notify the caller of new IPs.
                        If game = QueryGame.HalfLife Then
                            Me.OnInvokeQueryRawCallBack(callBack, New QueryRawAsyncEventArgs(gameServers.ToArray(), GameType.HalfLife))
                        Else
                            Me.OnInvokeQueryRawCallBack(callBack, New QueryRawAsyncEventArgs(gameServers.ToArray(), GameType.CounterStrikeSource))
                        End If
                    End If
                End If
            Loop While Not endOfQuery AndAlso Not Me.queryCancellationPending

            udpSocket.Close()

            If totalServers = 0 Then
                Me.OnQueryAsyncCompleted()
            End If
        End Sub

        ' This method will bind the socket to a portnumber.
        ' From investigation I have noticed that the master server wont reply
        ' if you use too low source port numbers. This is why we can not
        ' let the system assign a random port number for us.
        Private Sub BindSocket(ByVal udpSocket As Socket)
            Dim port As Integer = SourceMasterServer.PORT_LOWER_BOUND
            Dim successfullyBound As Boolean
            Do
                Try
                    udpSocket.Bind(New System.Net.IPEndPoint(System.Net.IPAddress.Any, port))
                    successfullyBound = True
                Catch ex As SocketException
                    If ex.SocketErrorCode = Net.Sockets.SocketError.AddressAlreadyInUse Then
                        successfullyBound = False
                    Else
                        Throw ex
                    End If
                Catch ex As Exception
                    Throw ex
                End Try
            Loop While Not successfullyBound

        End Sub

        ' This method is used when QueryAsync has been called. It acts as a callback for the
        ' RunAsyncQuery method. It will subsequently query each game server that it receives.
        Private Sub QueryAsync_CallBack(ByVal sender As Object, ByVal e As QueryRawAsyncEventArgs)
            For i As Integer = 0 To e.GameServers.Length - 1
                'Dim b As Boolean = System.Threading.ThreadPool.QueueUserWorkItem(AddressOf QueryGameServer, New Object() {e.GameServers(i), e.Type})
                Me.queryPool.AddQuery(AddressOf QueryGameServer, New Object() {e.GameServers(i), e.Type})
            Next
        End Sub

        ' This method acts as a callback to the QueueUserWorkItem call in the QueryAsync_CallBack method.
        Private Sub QueryGameServer(ByVal parameters As Object)
            If Not Me.queryCancellationPending Then
                Dim arguments() As Object = DirectCast(parameters, Object())

                Dim serverEndPoint As System.Net.IPEndPoint = DirectCast(arguments(0), System.Net.IPEndPoint)
                Dim server As New GameServer(serverEndPoint.Address.ToString(), serverEndPoint.Port, DirectCast(arguments(1), GameType))
                server.QueryServer()
                If server.IsOnline AndAlso Not Me.queryCancellationPending Then
                    Me.OnInvokeQueryCallBack(Me.asyncCallBack, New QueryAsyncEventArgs(server))
                End If
            End If
        End Sub

        ' This method will cancel any current query.
        Public Sub CancelAsyncQuery()
            Me.queryPool.CancelAll()
            Me.queryCancellationPending = True
        End Sub

        ' This method takes the reply from the master server, given as a byte array, and
        ' returns the IPEndPoints built from it.
        Private Function ParseReplyBuffer(ByVal buffer() As Byte, ByVal dataLength As Integer) As List(Of System.Net.IPEndPoint)
            Dim addresses As New List(Of System.Net.IPEndPoint)
            Dim bufferIndex As Integer = 6
            Do
                Dim ipep As New System.Net.IPEndPoint( _
                                New System.Net.IPAddress( _
                                New Byte() {buffer(bufferIndex), buffer(bufferIndex + 1), buffer(bufferIndex + 2), buffer(bufferIndex + 3)}), Me.GetUInt16NetworkOrder(buffer(bufferIndex + 4), buffer(bufferIndex + 5)))

                bufferIndex += 6

                addresses.Add(ipep)
            Loop Until bufferIndex >= dataLength

            Return addresses
        End Function

        ' This method randomly selects one of the IPs that the master servers hostname has been resolved too.
        Private Function GetMasterServerEndPoint(ByVal game As QueryGame) As System.Net.IPEndPoint
            Dim masterIPs() As System.Net.IPAddress
            Dim masterEndPoint As System.Net.IPEndPoint = Nothing
            Dim rand As New Random()

            Select Case game
                Case QueryGame.HalfLife
                    masterIPs = Me.ResolveHostname("hl1master.steampowered.com")

                    masterEndPoint = New System.Net.IPEndPoint(masterIPs(rand.Next(0, masterIPs.Length)), 27010)
                Case QueryGame.Source
                    masterIPs = Me.ResolveHostname("hl2master.steampowered.com")

                    masterEndPoint = New System.Net.IPEndPoint(masterIPs(rand.Next(0, masterIPs.Length)), 27011)
            End Select


            Return masterEndPoint
        End Function

        ' This method resolves the given hostname.
        Private Function ResolveHostname(ByVal host As String) As System.Net.IPAddress()
            Dim entry As System.Net.IPHostEntry = System.Net.Dns.GetHostEntry(host)
            Return entry.AddressList
        End Function

        Private Sub OnInvokeQueryRawCallBack(ByVal callback As QueryRawCallBack, ByVal e As QueryRawAsyncEventArgs)
            If Me.syncObject Is Nothing Then
                callback(Me, e)
            Else
                syncObject.Invoke(callback, New Object() {Me, e})
            End If
        End Sub

        Private Sub OnInvokeQueryCallBack(ByVal callback As QueryCallBack, ByVal e As QueryAsyncEventArgs)
            If Me.syncObject Is Nothing Then
                callback(Me, e)
            Else
                syncObject.Invoke(callback, New Object() {Me, e})
            End If
        End Sub

        Private Delegate Sub OnQueryAsyncCompletedDelegate()
        Private Sub OnQueryAsyncCompleted()
            If Me.syncObject Is Nothing OrElse Not Me.syncObject.InvokeRequired Then
                RaiseEvent QueryAsyncCompleted(Me, EventArgs.Empty)
            Else
                Me.syncObject.Invoke(New OnQueryAsyncCompletedDelegate(AddressOf Me.OnQueryAsyncCompleted), Nothing)
            End If
        End Sub

        ' This method determines if an address consists of only 0's.
        ' Such an address denotes the end of the IP list obtained by the master server.
        Private Function IsNullAddress(ByVal endpoint As System.Net.IPEndPoint) As Boolean
            Dim addressBytes() As Byte = endpoint.Address.GetAddressBytes
            Return addressBytes(0) = 0 AndAlso _
                    addressBytes(1) = 0 AndAlso _
                    addressBytes(2) = 0 AndAlso _
                    addressBytes(3) = 0 AndAlso _
                    endpoint.Port = 0
        End Function

        ' This method takes two bytes and returns them as an uint16.
        Private Function GetUInt16NetworkOrder(ByVal byte1 As Byte, ByVal byte2 As Byte) As UInt16
            ' Convert the first byte to a uint16.
            Dim value As UInt16 = Convert.ToUInt16(byte1)

            ' Shift it 8 bits to the left.
            value <<= 8

            ' Use bitwise OR to "place" the value of byte2 on the lower 8 bits of 'value'.
            value = value Or byte2

            Return value
        End Function

        ' This method builds a query using the given parameters.
        Private Function BuildQuery(ByVal region As QueryRegionCode, ByVal filter As QueryFilter, ByVal map As String, ByVal gameMod As String, ByVal ipSeed As String) As Byte()
            Dim queryBuilder As New List(Of Byte)

            ' Master server queries always begin with 49 (the character 1).
            queryBuilder.Add(49)

            ' Then follows the region code
            queryBuilder.Add(CByte(region))

            ' Then follows the IP and port seed, which initially will be 0.0.0.0:0
            queryBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes(ipSeed))

            ' A null-byte to terminate the previous string.
            queryBuilder.Add(0)

            ' And then follows the filter string:
            queryBuilder.AddRange(Me.BuildFilter(filter))

            ' Add the map and/or gamemod filters, if any.
            If map <> String.Empty Then
                queryBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes(String.Format("\map\{0}", map)))
            End If
            If gameMod <> String.Empty Then
                queryBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes(String.Format("\gamedir\{0}", gameMod)))
            End If

            ' This line will make sure we wont get any Left4Dead servers in our reply.
            ' If Left4Dead servers should be returned from the query, just remove this line.
            queryBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\napp\500"))

            ' And finally, terminate the previous string with a null-byte.
            queryBuilder.Add(0)

            Return queryBuilder.ToArray()
        End Function

        ' This method builds the filter to be used for the query, based on the given QueryFilter argument.
        Private Function BuildFilter(ByVal filter As QueryFilter) As Byte()
            Dim filterBuilder As New List(Of Byte)

            If (filter And QueryFilter.Dedicated) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\type\d"))
            End If
            If (filter And QueryFilter.AntiCheat) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\secure\1"))
            End If
            If (filter And QueryFilter.Linux) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\linux\1"))
            End If
            If (filter And QueryFilter.NotEmpty) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\empty\1"))
            End If
            If (filter And QueryFilter.NotFull) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\full\1"))
            End If
            If (filter And QueryFilter.SpectatorProxies) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\proxy\1"))
            End If
            If (filter And QueryFilter.Empty) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\noplayers\1"))
            End If
            If (filter And QueryFilter.WhiteListed) > 0 Then
                filterBuilder.AddRange(System.Text.Encoding.UTF8.GetBytes("\white\1"))
            End If

            Return filterBuilder.ToArray()
        End Function

        ' The SynchronizingObject is used when a callback is called.
        ' It invokes the callback on the thread that created the SynchronizingObject.
        ' The most common use is just to set this to the Form that will handle the callbacks.
        Public Property SynchronizingObject() As System.ComponentModel.ISynchronizeInvoke
            Get
                Return Me.syncObject
            End Get
            Set(ByVal value As System.ComponentModel.ISynchronizeInvoke)
                Me.syncObject = value
            End Set
        End Property

        Public Enum QueryGame
            HalfLife
            Source
        End Enum

        Public Enum QueryRegionCode
            USWestcoast = 1
            SouthAmerica
            Europe
            Asia
            Australia
            MiddleEast
            Africa
            All = 255
        End Enum

        Public Enum QueryFilter
            None = 1
            Dedicated = 2
            AntiCheat = 4
            Linux = 8
            NotEmpty = 16
            NotFull = 32
            SpectatorProxies = 64
            Empty = 128
            WhiteListed = 256
        End Enum

        Public Class QueryAsyncEventArgs
            Inherits EventArgs
            Private server As GameServer

            Private endOfReply As Boolean

            Public Sub New(ByVal server As GameServer)
                Me.server = server
            End Sub

            Public ReadOnly Property GameServer() As GameServer
                Get
                    Return Me.server
                End Get
            End Property
        End Class

        Public Class QueryRawAsyncEventArgs
            Inherits EventArgs
            Private servers() As System.Net.IPEndPoint
            Private typeOfGame As GameType

            Public Sub New(ByVal servers() As System.Net.IPEndPoint, ByVal typeOfGame As GameType)
                Me.servers = servers
                Me.typeOfGame = typeOfGame
            End Sub

            Public ReadOnly Property GameServers() As System.Net.IPEndPoint()
                Get
                    Return Me.servers
                End Get
            End Property

            Public ReadOnly Property Type() As GameType
                Get
                    Return Me.typeOfGame
                End Get
            End Property
        End Class

        Public Class NoReplyException
            Inherits Exception
            Public Sub New(ByVal message As String)
                MyBase.New(message)
            End Sub
        End Class
    End Class

End Namespace
