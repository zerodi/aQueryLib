#Region "Using directives"
Imports System
#End Region

Namespace aQueryLib
    Friend Class Source
        Inherits Protocol
        ' FF FF FF FF 57
        Private Const _QUERY_GETCHALLANGE As String = "ÿÿÿÿW"

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

        ''' Serverhost address
        ''' Serverport
        Public Sub New(ByVal host As String, ByVal port As Integer)
            MyBase._protocol = GameProtocol.Source
            Connect(host, port)
        End Sub

        ''' 
        ''' Querys the serverinfos
        ''' 
        Public Overloads Overrides Sub GetServerInfo()

            If Query(_QUERY_DETAILS) Then
                ParseDetails()
            Else
                Return
            End If

            If Query(_QUERY_PLAYERS & Chr(0) & Chr(0) & Chr(0) & Chr(0)) Then
                ' As long as the response is a challenge-number response,
                ' We need to re-send the query with the new challenge string.
                While Response(4) = _S2C_CHALLENGE_NUMBER
                    GetChallenge()
                    Query(_QUERY_PLAYERS & _CHALLANGE)
                End While
                ParsePlayers()
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
        End Sub

        Private Sub GetChallenge()
            ' Calling ToString on a byte array will only return
            ' System.Byte[], which is of no use for us.
            ' _CHALLANGE = Response.ToString()
            '
            ' Instead, we should just keep the challenge number,
            ' which is a 4 byte integer.
            _CHALLANGE = System.Text.Encoding.UTF8.GetString(Response, 5, 4)
        End Sub

        Private Sub ParseDetails()
            _params("protocolver") = Response(5).ToString()
            If Response(5) = 0 Then
                Return
            End If
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
            If Response(5) <> 15 Then ' Protocol version 15 seems to end here. So lets not go any further if thats the protocol of this reply.
                _params("servertype") = ChrW(Response(System.Threading.Interlocked.Increment(MyBase.Offset))).ToString()
                _params("serveros") = ChrW(Response(System.Threading.Interlocked.Increment(MyBase.Offset))).ToString()
                _params("passworded") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()
                _params("secured") = Response(System.Threading.Interlocked.Increment(MyBase.Offset)).ToString()

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

            End If
        End Sub

        Private Sub ParseRules()
            Dim key As String, val As String
            Dim ruleCount As Integer = BitConverter.ToInt16(Response, 5)
            MyBase.Offset = 7

            For i As Integer = 0 To ruleCount - 1
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

            Dim pNr As Integer = 0
            ' The number of players reported as playing on the server (given by the variable numPlayers)
            ' apparently isnt necessarily the same as the number of players reported in the response.
            ' Thats why a For-loop wont work here, instead we'll have to use a While-loop.
            Do While pNr < numPlayers - 2 AndAlso MyBase.Offset < Response.Length
                pNr = _players.Add(New Player())
                _players(pNr).Parameters.Add("playernr", Response(MyBase.Offset).ToString()) ' Removed the Math.Max here. It shouldnt be needed.
                MyBase.Offset += 1 'Increment the offset AFTER getting the playernr, not before.
                _players(pNr).Name = ReadNextParam()
                _players(pNr).Score = BitConverter.ToInt32(Response, Offset)
                MyBase.Offset += 4
                _players(pNr).Time = New TimeSpan(0, 0, CInt(BitConverter.ToSingle(Response, Offset)))
                MyBase.Offset += 4
            Loop
        End Sub
    End Class
End Namespace
