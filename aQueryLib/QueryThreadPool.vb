Public Class QueryThreadPool
    ' The structure that this class uses internally to store queued callbacks and states.

    Private Structure InternalQueueItem
        Public callback As System.Threading.WaitCallback
        Public state As Object

        Public Sub New(ByVal callback As System.Threading.WaitCallback, ByVal state As Object)
            Me.callback = callback
            Me.state = state
        End Sub
    End Structure

    Private queryQueue As Queue(Of InternalQueueItem) ' This is our queue for the query callbacks.
    Private queryThreads As List(Of System.ComponentModel.BackgroundWorker) ' This is our list of ongoing queries.
    Private simultaneousQueries As Integer ' This variable tells us how many simultaneous queries are allowed

    Public Event AllQueriesProcessed(ByVal sender As Object, ByVal e As EventArgs)

    ''' <summary>
    ''' Creates a new QueryThreadPool instance.
    ''' </summary>
    ''' <param name="simultaneousQueries">The total amount of simultaneous queries.</param>
    ''' <remarks></remarks>
    Sub New(ByVal simultaneousQueries As Integer)
        Me.simultaneousQueries = simultaneousQueries
        Me.queryThreads = New List(Of System.ComponentModel.BackgroundWorker)(Me.simultaneousQueries)
        Me.queryQueue = New Queue(Of InternalQueueItem)
    End Sub

    ''' <summary>
    ''' Adds a query to the queue. The query will be processed as soon
    ''' as there are threads available.
    ''' </summary>
    ''' <param name="callback">The callback to the query</param>
    ''' <param name="state">The arguments to be passed to the callback</param>
    ''' <remarks></remarks>
    Public Sub AddQuery(ByVal callback As System.Threading.WaitCallback, ByVal state As Object)
        Me.queryQueue.Enqueue(New InternalQueueItem(callback, state))
        Me.checkFreeThreads()
    End Sub

    ' This method will check if there are any free threads for querying.
    Private Sub checkFreeThreads()
        If Me.queryQueue.Count > 0 Then
            If Me.queryThreads.Count < Me.simultaneousQueries Then
                Dim nextItem As InternalQueueItem = Me.queryQueue.Dequeue()
                Dim worker As New System.ComponentModel.BackgroundWorker()
                AddHandler worker.DoWork, AddressOf Me.BackgroundWorker_DoWork
                AddHandler worker.RunWorkerCompleted, AddressOf Me.BackgroundWorker_WorkCompleted
                worker.RunWorkerAsync(nextItem)
                queryThreads.Add(worker)
            End If
        Else
            RaiseEvent AllQueriesProcessed(Me, EventArgs.Empty)
        End If

    End Sub

    ' This method cancels all pending queries.
    Public Sub CancelAll()
        'We cant do much about the workers that we have already started.
        'But we can clear the queryQueue to make sure no more gets started.
        Me.queryQueue.Clear()
        RaiseEvent AllQueriesProcessed(Me, EventArgs.Empty)
    End Sub

    ' This is our internal DoWork handler.
    Private Sub BackgroundWorker_DoWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs)
        Dim workItem As InternalQueueItem = DirectCast(e.Argument, InternalQueueItem)
        workItem.callback(workItem.state)
    End Sub

    ' This is our internal WorkCompleted handler. It will be invoked when a query is completed.
    Private Sub BackgroundWorker_WorkCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs)
        Me.queryThreads.Remove(DirectCast(sender, System.ComponentModel.BackgroundWorker))
        Me.checkFreeThreads()
    End Sub


End Class