#Region "Using directives"
Imports System
#End Region

Namespace aQueryLib
    Friend Class HalfLife
        Inherits aQueryLib.Protocol
#Region "Querystrings"
        ' FF FF FF FF 54 53 6F 75 72 63 65 20 45 6E 67 69
        ' 6E 65 20 51 75 65 72 79 00       
        Private Const _QUERY_DETAILS As String = "ÿÿÿÿTSource Engine Query" & Chr(0) ' Changed this from just ÿÿÿÿT.

        ' FF FF FF FF 56
        Private Const _QUERY_RULES As String = "ÿÿÿÿV"

        ' FF FF FF FF 55
        Private Const _QUERY_PLAYERS As String = "ÿÿÿÿU"

        ' This value is used to determine if a reply is an "S2C_CHALLENGE", ie if it contains
        ' a new challenge number for us to use.
        Private Const _S2C_CHALLENGE_NUMBER As Byte = 65

        ' This value is used to determine if a reply is a reply to a A2S_RULES query.
        Private Const _A2S_RULES_NUMBER As Byte = 69

        ' This value is used to determine if a reply is a reply to a A2S_PLAYER query.
        Private Const _A2S_PLAYER_NUMBER As Byte = 68

        Private _CHALLANGE As String
#End Region

        ''' <param name="host">Serverhost address</param>
        ''' <param name="port">Serverport</param>
        Public Sub New(ByVal host As String, ByVal port As Integer)
            MyBase._protocol = GameProtocol.HalfLife
            Connect(host, port)
        End Sub

        ''' <summary>
        ''' Querys the serverinfos
        ''' </summary>
        Public Overloads Overrides Sub GetServerInfo()
            'If Not IsOnline Then
            '    Exit Sub
            'End If

            If Query(_QUERY_DETAILS) Then
                ParseDetails()
            Else
                Return
            End If

            If Query(_QUERY_RULES & Chr(0) & Chr(0) & Chr(0) & Chr(0)) Then
                ' As long as the response is a challenge-number response,
                ' We need to re-send the query with the new challenge string.
                While Response(4) = _S2C_CHALLENGE_NUMBER
                    GetChallenge()
                    Query(_QUERY_RULES & _CHALLANGE)
                End While
                ParseRules()
            End If

            If Query(_QUERY_PLAYERS & Chr(0) & Chr(0) & Chr(0) & Chr(0)) Then
                While Response(4) = _S2C_CHALLENGE_NUMBER
                    GetChallenge()
                    Query(_QUERY_PLAYERS & _CHALLANGE)
                End While
                ParsePlayers()
            End If
        End Sub

        Private Sub GetChallenge()
            ' Calling ToString on a byte array will only return
            ' System.Byte[], which is of no use for us.
            ' _CHALLANGE = Response.ToString()
            '
            ' Instead, we should just keep the challenge number,
            ' which is a 4 byte integer.
            _CHALLANGE = System.Text.Encoding.[Default].GetString(Response, 5, 4)
        End Sub

        ''' <summary>
        ''' Gets the active modification
        ''' </summary>
        Public Overloads Overrides ReadOnly Property [Mod]() As String
            Get
                Return _params("mod")
            End Get
        End Property

#Region "Private methods"
        Private Sub ParseDetails()
            Offset = 6
            ' Goldsource servers can respond either in the "new" way (that is, in the same manner as Source servers),
            ' or in the "old" way. Before going any furter we need to find out
            ' what type of response we have got.
            ' If the byte on index 5 is equal to 'I' (0x49), the reply follows the new protocol standard,
            ' but if the byte equals 'm' (0x6D), it does not.
            If Response(4) = 73 Then
                ' Reply follows new protocol standard.
                Me.ParseDetailsSource()
            Else
                ' Reply follows old protocol standard.

                Dim nextParam As String = ReadNextParam()
                MyBase.Offset = 6
                Dim indexColon As Integer = nextParam.IndexOf(":"c)

                If Response(5) = 0 Then
                    ' Undocumented protocol version.
                    ' Unsupported for now.
                    _isOnline = False
                    Return
                Else
                    If indexColon = -1 Then
                        ' String contained no colon, so it cant have been
                        ' An ip address with port number.
                        Me.ParseDetailsSource()
                    Else
                        Me.ParseDetailsGoldSource()
                    End If
                End If


            End If
        End Sub

        Private Sub ParseDetailsSource()
            _params("protocolver") = Response(5).ToString()
            _params("hostname") = ReadNextParam(6)
            _params("mapname") = ReadNextParam()
            _params("mod") = ReadNextParam()
            _params("modname") = ReadNextParam()

            ' The field that denotes the number of players on the server is not necessarily always on this index (Response.Length - 7), therefor the variable i is not accurate.
            ' The next field in the response now is the AppID field (2 byte long), which is a unique ID for all Steam applications.
            ' I'm not sure you want to include it or not, but for now I will.
            'Dim i As Integer = Response.Length - 7
            _params("appid") = (Response(MyBase.Offset) Or (CShort(Response(System.Threading.Interlocked.Increment(MyBase.Offset)) << 8))).ToString() ' Perform binary OR to get the short value from the two bytes.
            _params("players") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()
            _params("maxplayers") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()
            _params("botcount") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()
            _params("servertype") = ChrW(Response(System.Threading.Interlocked.Increment(MyBase.Offset))).ToString()
            _params("serveros") = ChrW(Response(System.Threading.Interlocked.Increment(MyBase.Offset))).ToString()
            _params("passworded") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()
            _params("secureserver") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()

            MyBase.Offset += 1 'Increment the offset to take the last read byte into account.

            _params("gameversion") = ReadNextParam()
            ' In most cases, the reply will end here. But some servers include an Extra Data Flag along
            ' with some extra data, so if we'll read that too, if available.

            If MyBase.Offset < Response.Length Then
                Dim flag As Byte = Response(MyBase.Offset)
                MyBase.Offset += 1
                If (flag And &H80) > 0 Then '  	The server's game port # is included
                    _params("serverport") = CStr(BitConverter.ToInt16(Response, MyBase.Offset))
                    MyBase.Offset += 2
                End If


                If (flag And &H40) > 0 Then ' The spectator port # and then the spectator server name are included
                    _params("spectatorport") = CStr(BitConverter.ToInt16(Response, MyBase.Offset))
                    MyBase.Offset += 2
                    _params("spectatorname") = ReadNextParam()
                End If

                If (flag And &H20) > 0 Then ' The game tag data string for the server is included [future use]
                    _params("gametagdata") = ReadNextParam()
                End If
            End If
        End Sub

        Private Sub ParseDetailsGoldSource()
            _params("serveraddress") = ReadNextParam()
            _params("hostname") = ReadNextParam()
            _params("mapname") = ReadNextParam()
            _params("mod") = ReadNextParam()
            _params("modname") = ReadNextParam()
            ' Removed a bunch of System.Math.Max calls here, just like in the Source-class.
            _params("playernum") = Response(Offset).ToString()
            _params("maxplayers") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()
            _params("protocolver") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()

            _params("servertype") = ChrW(Response(System.Threading.Interlocked.Increment(Offset))).ToString()
            _params("serveros") = ChrW(Response(System.Threading.Interlocked.Increment(Offset))).ToString()
            _params("passworded") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()
            _params("modded") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()

            If Response(Offset) = 1 Then
                _params("modwebpage") = ReadNextParam()
                _params("moddlserver") = ReadNextParam()
                Offset += 1 ' Skip the extra null byte here.

                ' Certain servers doesnt seem to include this part so we'll
                ' check to make sure it does before trying to read it.
                If Response.Length > MyBase.Offset + 10 Then
                    _params("modversion") = BitConverter.ToInt32(Response, Offset).ToString()
                    Offset += 4
                    _params("modsize") = BitConverter.ToInt32(Response, Offset).ToString()
                    Offset += 4
                    _params("serversidemod") = Response(Offset).ToString()
                    _params("modcustomclientdll") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()

                End If
            End If

            ' As with the block of code above, I'm finding that certain servers just doesnt return this
            ' information so we'll make sure it does before trying to read it.
            If Response.Length > MyBase.Offset + 2 Then
                _params("secured") = Response(System.Threading.Interlocked.Increment(Offset)).ToString()
                _params("botcount") = Response(Offset).ToString()
            End If

        End Sub

        Private Sub ParseRules()
            Dim key As String, val As String
            Offset = 7

            For i As Integer = 0 To (BitConverter.ToInt16(Response, 5) * 2) - 1
                key = ReadNextParam()
                val = ReadNextParam()
                If key.Length = 0 Then
                    Continue For
                End If
                _params(key) = val
            Next
        End Sub

        Private Sub ParsePlayers()
            If Response(4) <> 68 Then ' 68 = 'D'
                Return
            End If
            Dim numPlayers As Byte = Response(5)

            _params("numplayers") = numPlayers.ToString()
            MyBase.Offset = 6
            If numPlayers > 0 Then

                Dim pNr As Integer = 0
                ' The number of players reported as playing on the server (given by the variable numPlayers)
                ' apparently isnt necessarily the same as the number of players reported in the response.
                ' Thats why a For-loop wont work here, instead we'll have to use a While-loop.
                Do
                    pNr = _players.Add(New Player())
                    _players(pNr).Parameters.Add("playernr", Response(MyBase.Offset).ToString()) ' Removed the Math.Max here. It shouldnt be needed.
                    MyBase.Offset += 1 'Increment the offset AFTER getting the playernr, not before.
                    _players(pNr).Name = ReadNextParam()
                    _players(pNr).Score = BitConverter.ToInt32(Response, Offset)
                    MyBase.Offset += 4
                    _players(pNr).Time = New TimeSpan(0, 0, CInt(BitConverter.ToSingle(Response, Offset)))
                    MyBase.Offset += 4
                Loop While pNr < numPlayers - 1 AndAlso MyBase.Offset < Response.Length
            End If
        End Sub
#End Region
    End Class
End Namespace

