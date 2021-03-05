#Region "Using directives"
Imports System
#End Region

Namespace aQueryLib
    ''' <summary>
    ''' Gameserver types
    ''' </summary>
    Public Enum GameType
#Region "Half-Life"
        ''' <summary>
        ''' Half-Life
        ''' </summary>
        HalfLife = GameProtocol.HalfLife
        ''' <summary>
        ''' Counter-Strike: v1.6
        ''' </summary>
        CounterStrike_16 = GameProtocol.HalfLife
        ''' <summary>
        ''' Counter-Strike: Condition Zero
        ''' </summary>
        CounterStrike_ConditionZero = GameProtocol.HalfLife
        ''' <summary>
        ''' Day of Defeat
        ''' </summary>
        DayOfDefeat = GameProtocol.HalfLife
        ''' <summary>
        ''' Gunman Chronicles
        ''' </summary>
        GunmanChronicles = GameProtocol.HalfLife
#End Region
#Region "Hl-Source"
        ''' <summary>
        ''' Source Engine (Generic Protocol)
        ''' </summary>
        Source = GameProtocol.Source
        ''' <summary>
        ''' Half-Life 2
        ''' </summary>
        HalfLife2 = GameProtocol.Source
        ''' <summary>
        ''' Counter-Strike: Source
        ''' </summary>
        CounterStrikeSource = GameProtocol.Source
#End Region
        ''' <summary>
        ''' Not listed game
        ''' </summary>
        Unknown = GameProtocol.None
    End Enum

    ''' <summary>
    ''' Gameserver protocol
    ''' </summary>
    Public Enum GameProtocol
        ''' <summary>
        ''' Halflife and HL-Mods
        ''' </summary>
        HalfLife
        ''' <summary>
        ''' Halflife Source
        ''' </summary>
        Source
        ''' <summary>
        ''' Unknown
        ''' </summary>
        None
    End Enum
End Namespace