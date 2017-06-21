namespace SqlFun

module Sql =


    type Clause = 
        | SELECT of string
        | FROM of string
        | WHERE of string
        | JOIN of string * string
        | LEFT_JOIN of string * string
        | RIGHT_JOIN of string * string
        | ORDER_BY of string
        | GROUP_BY of string
        | HAVING of string
        | UNION of Clause seq


    type Statement = Clause seq


    let toJoin (joinType: string * string -> Clause) (condition: string) (query: Statement) = 
        let tableName = query 
                        |> Seq.collect (function FROM name -> [name] | _ -> []) 
                        |> Seq.head
        let filtered = query 
                        |> Seq.filter (function FROM _ -> false | _ -> true)
        Seq.append filtered [joinType (tableName, condition)]
    
    let inline addClause pattern content query = 
        if not (System.String.IsNullOrEmpty content)
        then query + (sprintf pattern content)
        else query
    

    let toSql (query: Statement) = 
        let columns = query 
                        |> Seq.collect (function SELECT name -> [name] | _ -> [])
                        |> String.concat ", "
        let tables = query
                        |> Seq.collect (function FROM name -> [name] | _ -> [])
                        |> String.concat ", "
        let joins = query
                        |> Seq.collect (function 
                                        | JOIN (table, condition) -> [sprintf "JOIN %s ON %s" table condition]
                                        | LEFT_JOIN  (table, condition) -> [sprintf "LEFT JOIN %s ON %s" table condition]
                                        | RIGHT_JOIN (table, condition) -> [sprintf "RIGHT JOIN %s ON %s" table condition]
                                        | _ -> [])
                        |> String.concat "\n"
        let whereClauses = query
                            |> Seq.collect (function WHERE cnd -> ["(" + cnd + ")"] | _ -> [])
                            |> String.concat " AND "
        // Nieprawidłowe
        let orderByCols = query
                            |> Seq.collect (function ORDER_BY cols -> [cols] | _ -> [])
                            |> String.concat ", "
        // Nieprawidłowe
        let groupByCols = query
                            |> Seq.collect (function GROUP_BY cols -> [cols] | _ -> [])
                            |> String.concat ", "
        let havingClauses = query
                            |> Seq.collect (function HAVING cnd -> ["(" + cnd + ")"] | _ -> [])
                            |> String.concat " AND "
    
        sprintf "SELECT %s\nFROM %s" columns tables 
        |> addClause "\n%s" joins
        |> addClause "\nWHERE %s" whereClauses
        |> addClause "\nORDER BY %s" orderByCols
        |> addClause "\nGROUP BY %s" groupByCols
        |> addClause "\nHAVING %s" havingClauses
    
    



