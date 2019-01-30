namespace SqlFun.Performance.Tests

module HandCodedSuite = 
    open Data
    open System
    open System.Data


    let private materializePost (reader: IDataReader) = 
        {
            id = reader.GetInt32(0)
            blogId = reader.GetInt32(1)
            name = reader.GetString(2)
            title = reader.GetString(3)
            content = reader.GetString(4)
            author = reader.GetString(5)
            createdAt = reader.GetDateTime(6)
            modifiedAt = if reader.IsDBNull(7) then None else Some (reader.GetDateTime(7))
            modifiedBy = if reader.IsDBNull(8) then None else Some (reader.GetString(8)) 
            status = reader.GetString(9)
            comments = []
        }

    let readMany() = 
        use connection = createConnection()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <- "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @id"
        command.Parameters.AddWithValue("@id", 1) |> ignore
        use reader = command.ExecuteReader()
        let values = [ while reader.Read() do yield materializePost reader ]
        ()

            
    let readOneManyTimes() = 
        use connection = createConnection()
        connection.Open()
        let rnd = Random()
        for i in 1..500 do
            use command = connection.CreateCommand()
            command.CommandText <- "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @id"
            let id = rnd.Next(1, 500)
            command.Parameters.AddWithValue("@id", id) |> ignore
            use reader = command.ExecuteReader() 
            let post = 
                if reader.Read() then
                    Some <| materializePost reader
                else
                    None
            ()


    module Async = 

        let readMany() = 
            async {
                use connection = createConnection()
                connection.Open()
                use command = connection.CreateCommand()
                command.CommandText <- "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @id"
                command.Parameters.AddWithValue("@id", 1) |> ignore
                use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
                let values = [ while reader.Read() do yield materializePost reader ]
                return ()
            }
        
            
        let readOneManyTimes() = 
            async {
                use connection = createConnection()
                connection.Open()
                let rnd = Random()
                for i in 1..500 do
                    use command = connection.CreateCommand()
                    command.CommandText <- "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @id"
                    let id = rnd.Next(1, 500)
                    command.Parameters.AddWithValue("@id", id) |> ignore
                    use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
                    let post = 
                        if reader.Read() then
                            Some <| materializePost reader
                        else
                            None
                    ()
            }
