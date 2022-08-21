namespace SqlFun

open System
open System.Linq.Expressions
open System.Data.Common
open System.Data
open Microsoft.FSharp.Reflection
open System.Threading.Tasks
open System.Reflection

open SqlFun.Types
open SqlFun.ExpressionExtensions

type RowBuilder = ParameterExpression -> Map<string, int * Type> -> string -> string -> Type -> Expression

/// <summary>
/// Builds result sequence row by row, as client code requests data.
/// Allows to read very large datasets without consuming too much memory.
/// Keeps open data reader until the data is read to end or the stream object is disposed.
/// The code reading from ResultStream must be placed in context of open connection.
/// Can not be used in multi-result queries or as record fields.
/// </summary>
type ResultStream<'t>(reader: IDataReader, builder: IDataReader -> 't) = 

    let data = seq {
        while reader.Read() do yield builder reader
        reader.Dispose()
    }

    interface seq<'t> with
        member __.GetEnumerator(): Collections.Generic.IEnumerator<'t> = 
            data.GetEnumerator()
        member __.GetEnumerator(): Collections.IEnumerator = 
            (data :> Collections.IEnumerable).GetEnumerator() 

    interface IDisposable with
        member __.Dispose(): unit = 
            reader.Dispose()

module ResultBuilder = 

    let private dataRecordColumnAccessMethodByType = 
        [
            typeof<Boolean>,    "GetBoolean"
            typeof<Byte>,       "GetByte"
            typeof<Char>,       "GetChar"
            typeof<DateTime>,   "GetDateTime"
            typeof<Decimal>,    "GetDecimal"
            typeof<Double>,     "GetDouble"
            typeof<float>,      "GetFloat"
            typeof<Guid>,       "GetGuid"
            typeof<Int16>,      "GetInt16"
            typeof<Int32>,      "GetInt32"
            typeof<Int64>,      "GetInt64" 
            typeof<string>,     "GetString"
        ] 
        |> List.map (fun (t, name) -> t, typeof<IDataRecord>.GetMethod(name))

    let private dataRecordGetValueMethod = typeof<IDataRecord>.GetMethod("GetValue")


    let private mapReaderToList (f: IDataReader -> 't) (r: IDataReader):'t list =
        [ while r.Read() do yield f r ]

    let private mapReaderToListAsync f r = async { return mapReaderToList f r }

    let private mapReaderToArray (f: IDataReader -> 't) (r: IDataReader):'t array =
        [| while r.Read() do yield f r |]

    let private mapReaderToArrayAsync f r = async { return mapReaderToArray f r }

    let private mapReaderToSet (f: IDataReader -> 't) (r: IDataReader):'t Set =
        seq { while r.Read() do yield f r } |> Set.ofSeq

    let private mapReaderToSetAsync f r = async { return mapReaderToSet f r }

    let private mapReaderToSeq (f: IDataReader -> 't) (r: IDataReader):'t seq =
        [ while r.Read() do yield f r ] |> List.toSeq

    let private mapReaderToSeqAsync f r = async { return mapReaderToSeq f r }

    type StructuredMetadata = 
        | Direct of Map<string, (int * Type)>
        | Nested of StructuredMetadata list

    let adaptToReturnType metadata returnType = 

        let rec makeTree metadata returnType = 
            match returnType with
            | Tuple elts ->
                elts |> Seq.map (makeTree metadata)
                     |> List.ofSeq
                     |> Nested
            | _ ->
                let head = List.head !metadata 
                metadata := List.tail !metadata
                Direct head  

        makeTree (ref metadata) returnType        

    type Toolbox() = 

        static member BuildSingleResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't>(fun reader -> if reader.Read() then resultBuilder.Invoke(reader) else failwith "Value does not exist. Use option type.")
        
        static member BuildOptionalResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't option>(fun reader -> if reader.Read() then Some (resultBuilder.Invoke(reader)) else None)

        static member BuildListResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't list>(mapReaderToList resultBuilder.Invoke)

        static member BuildSeqResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't seq>(mapReaderToSeq resultBuilder.Invoke)

        static member BuildArrayResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't array>(mapReaderToArray resultBuilder.Invoke)

        static member BuildSetResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDataReader, 't Set>(mapReaderToSet resultBuilder.Invoke)

        static member BuildStreamResult (resultBuilder: Func<IDataReader, 't>) = 
            Func<IDbCommand, 't ResultStream>(fun command ->
                let reader = command.ExecuteReader()
                new ResultStream<'t>(reader, resultBuilder.Invoke))

        static member BuildSingleResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t>>(fun reader -> 
                async {
                    let! read = Async.AwaitTask(reader.ReadAsync())
                    if read 
                    then return resultBuilder.Invoke(reader) 
                    else return failwith "Value does not exist. Use option type."
                })        

        static member BuildOptionalResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t option>>(fun (reader: DbDataReader) -> 
                async {
                    let! read = Async.AwaitTask(reader.ReadAsync())
                    if read 
                    then return Some(resultBuilder.Invoke(reader))
                    else return None
                })

        static member BuildListResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t list>>(fun (reader: DbDataReader) -> reader |> mapReaderToListAsync resultBuilder.Invoke)

        static member BuildArrayResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t array>>(fun (reader: DbDataReader) -> reader |> mapReaderToArrayAsync resultBuilder.Invoke)

        static member BuildSetResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t Set>>(fun (reader: DbDataReader) -> reader |> mapReaderToSetAsync resultBuilder.Invoke)

        static member BuildSeqResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbDataReader, Async<'t seq>>(fun (reader: DbDataReader) -> reader |> mapReaderToSeqAsync resultBuilder.Invoke)

        static member BuildStreamResultAsync (resultBuilder: Func<IDataReader, 't>) = 
            Func<DbCommand, Async<'t ResultStream>>(fun (command: DbCommand) -> async {
                let! reader = Async.AwaitTask(command.ExecuteReaderAsync())
                return new ResultStream<'t>(reader, resultBuilder.Invoke)
            })                
            

        static member AsyncBind (v: 't Async, f: Func<'t, 'u Async>) =
            async.Bind(v, f.Invoke)

        static member TaskBind (v: Task<'t>, f: Func<'t, 'u Async>) =
            async.Bind(Async.AwaitTask(v), f.Invoke)

        static member AsyncReturn  (v: 't) =
            async.Return(v)

        static member NewList(): 't list =
            List.empty

        static member NewArray(): 't array =
            Array.empty

        static member NewSet(): 't Set =
            Set.empty

        static member NewSeq(): 't seq =
            Seq.empty

        static member IntToEnum(value: int): 't =
                LanguagePrimitives.EnumOfValue value

        static member ThrowOnInvalidEnumValue(value: 'v): 't = 
            raise (ArgumentException("The " + value.ToString() + " value can not be converted to " + typeof<'t>.Name + " type."))


    type Expression with
    
        static member Call (genericParams: Type array, methodName: string, [<ParamArray>]arguments: Expression array ) =
            Expression.Call(typeof<Toolbox>, genericParams, methodName, arguments)


    let private getFieldPrefix (field: PropertyInfo) = 
        field.GetCustomAttributes<PrefixedAttribute>() 
        |> Seq.map (fun a -> if a.Name <> "" then a.Name else field.Name)
        |> Seq.fold (fun last next -> next) ""

    let typeNotSupported t = 
        failwithf "Type not supported: %O" t

    let coerce colType (targetType: Type) (expr: Expression) = 
        match targetType with
        | EnumOf (eType, values) ->
            if values |> Seq.exists (fun (e, v) -> e <> v)
            then        
                let comparer v = 
                    if eType = expr.Type then
                        Expression.Equal(Expression.Constant(v, eType), expr) :> Expression
                    else
                        let exprAsObj = Expression.Convert(expr, typeof<obj>)
                        Expression.Call(Expression.Constant(v), "Equals", exprAsObj) :> Expression
                let exprAsEnum = Expression.Call([| expr.Type; targetType|], "ThrowOnInvalidEnumValue", expr) :> Expression
                values 
                |> Seq.fold (fun cexpr (e, v) -> Expression.Condition(comparer v, Expression.Constant(e), cexpr) :> Expression) exprAsEnum
            else 
                Expression.Call([| targetType |], "IntToEnum", expr) :> Expression
        | _ when targetType = colType ->
            expr
        | _ -> Expression.Convert(expr, targetType) :> Expression


    let private getByteArray = 
        Func<IDataReader, int, byte[]>(fun reader ordinal ->
            let length = reader.GetBytes(ordinal, 0L, null, 0, 0)
            if length = 0L then [||]
            else
                let buffer = Array.zeroCreate(int32(length))
                reader.GetBytes(ordinal, 0L, buffer, 0, Int32.MaxValue) |> ignore
                buffer)


    let private buildColumnAccessor (reader: ParameterExpression) colType ordinal targetType = 
        let call = 
            if colType = typeof<byte[]> then
                Expression.Invoke(Expression.Constant(getByteArray), reader, Expression.Constant(ordinal)) :> Expression
            else
                let accessMethod = dataRecordColumnAccessMethodByType |> List.tryFind (fst >> (=) colType) 
                                   |> Option.map snd
                                   |> Option.defaultValue dataRecordGetValueMethod
                Expression.Call(reader, accessMethod, Expression.Constant(ordinal)) :> Expression
        if isOption targetType
        then 
            Expression.Condition(
                Expression.Call(reader, "IsDBNull", Expression.Constant(ordinal)),                              
                Expression.GetNoneUnionCase targetType,
                Expression.GetSomeUnionCase targetType (coerce colType (getUnderlyingType targetType) call)) :> Expression  
        else
            coerce colType targetType call
  

    let rec private checkColumnExistence reader metadata prefix returnType: string list * string list = 
        match returnType with
        | OptionOf t -> 
            checkColumnExistence reader metadata prefix t
        | Tuple items -> 
            items |> Seq.map (checkColumnExistence reader metadata prefix)
                  |> Seq.reduce (fun (l1, l2) (r1, r2) -> l1 @ r1, l2 @ r2)
        | CollectionOf _ ->
            [], []
        | Record fields ->
            fields |> Seq.map (checkSingleField reader metadata prefix)
                   |> Seq.reduce (fun (l1, l2) (r1, r2) -> l1 @ r1, l2 @ r2) 
        | t -> typeNotSupported t
    and
        private checkSingleField (reader: Expression) (metadata: Map<string, int * Type>) (prefix: string) (field: PropertyInfo) = 
            
            let rec checkWithType fieldType = 
                match fieldType with
                | Record _ | Tuple _ | CollectionOf _ ->
                    checkColumnExistence reader metadata (prefix + getFieldPrefix field) field.PropertyType
                | SimpleType -> 
                    if metadata |> Map.containsKey (prefix.ToLower() + field.Name.ToLower())
                    then [prefix.ToLower() + field.Name.ToLower()], []
                    else [], [prefix.ToLower() + field.Name.ToLower()]
                | OptionOf t ->
                    checkWithType t
                | t -> typeNotSupported t

            checkWithType field.PropertyType



    let rec private buildRowNullCheck reader metadata prefix returnType: Expression = 
        match returnType with
        | OptionOf t -> 
            buildRowNullCheck reader metadata prefix t
        | Tuple items -> 
            items |> Seq.map (buildRowNullCheck reader metadata prefix)
                  |> Seq.reduce (fun left right -> Expression.And(left, right) :> Expression)
        | CollectionOf _ ->
            Expression.Constant(true) :> Expression
        | Record fields ->
            fields |> Seq.map (buildFieldValueCheck reader metadata prefix)
                   |> Seq.reduce (fun left right -> Expression.And(left, right) :> Expression) 
        | t -> typeNotSupported t
    and
        private buildFieldValueCheck (reader: Expression) (metadata: Map<string, int * Type>) (prefix: string) (field: PropertyInfo) = 
            
            let rec checkWithType fieldType = 
                match fieldType with
                | Record _ | Tuple _ | CollectionOf _ ->
                    buildRowNullCheck reader metadata (prefix + getFieldPrefix field) field.PropertyType
                | SimpleType -> 
                    try
                        let (ordinal, _) = metadata |> Map.find (prefix.ToLower() + field.Name.ToLower())
                        Expression.Call(reader, "IsDBNull", Expression.Constant(ordinal)) :> Expression        
                    with ex ->
                        raise (Exception(sprintf "No column found for %s field. Expected: %s%s" field.Name prefix field.Name, ex))
                | OptionOf t ->
                    checkWithType t
                | t -> typeNotSupported t

            checkWithType field.PropertyType

    let private getEmptyCollectionBuilderName (collectionType: Type) = 
        if collectionType.IsArray then 
            "NewArray"
        else
            [ typedefof<list<_>>, "NewList"; typedefof<Set<_>>, "NewSet"; typedefof<seq<_>>, "NewSeq" ]
            |> List.tryFind (fst >> ((=) (collectionType.GetGenericTypeDefinition()))) 
            |> Option.map snd
            |> Option.defaultWith (fun () -> failwith (sprintf "Unsupported collection type %s" collectionType.FullName))

    let rec getRowBuilderExpression nextRB reader metadata (prefix: string) (fieldName: string) returnType =
        match returnType with
        | SimpleType | SimpleTypeOption ->
            try 
                let (ordinal, colType) = metadata |> Map.find (prefix.ToLower() + fieldName.ToLower())
                buildColumnAccessor reader colType ordinal returnType
            with ex ->
                raise (Exception(sprintf "No column found for %s field. Expected: %s%s" fieldName prefix fieldName, ex))
        | OptionOf retType ->
            match checkColumnExistence reader metadata prefix returnType with
            | _, [] ->
                Expression.Condition(
                    buildRowNullCheck reader metadata prefix returnType,
                    Expression.GetNoneUnionCase returnType, 
                    Expression.GetSomeUnionCase returnType (nextRB reader metadata prefix "" retType)) :> Expression
            | [], _ ->
                Expression.GetNoneUnionCase returnType :> Expression
            | existingCols, missingCols ->
                failwithf "Missing columns in results: %s" (missingCols |> String.concat ", ")
        | Tuple elts ->
            let accessors = elts 
                            |> Seq.map (nextRB reader metadata prefix "") 
                            |> List.ofSeq                
            Expression.NewTuple(accessors)
        | CollectionOf t ->
            Expression.Call([| t |], getEmptyCollectionBuilderName returnType) :> Expression
        | Record fields ->
            let accessors = 
                fields
                |> Seq.map (fun field -> nextRB reader metadata (prefix + getFieldPrefix field) field.Name field.PropertyType)
                |> List.ofSeq
            Expression.NewRecord(returnType, accessors)
        | t -> typeNotSupported t


    let private buildRow (rowBuilder: RowBuilder) metadata returnType =     
        let reader = Expression.Parameter(typeof<IDataReader>, "reader")
        let builder = rowBuilder reader metadata "" "" returnType
        Expression.Lambda(builder, reader)


    let private buildScalar metadata returnType =
        let colType = metadata |> Map.toSeq |> Seq.map snd |> Seq.find (fst >> (=) 0) |> snd
        let reader = Expression.Parameter(typeof<IDataReader>, "reader")
        let getter = buildColumnAccessor reader colType 0 returnType
        Expression.Lambda(getter, reader)

    let commonCollectionResultBuilders = 
        [   typedefof<list<_>>.Name, "BuildListResult"
            typedefof<Set<_>>.Name, "BuildSetResult"
            typedefof<seq<_>>.Name, "BuildSeqResult"            
        ] |> Map.ofList 

    let multiResultCollectionBuilders = commonCollectionResultBuilders

    let singleResultCollectionBuilders = 
        commonCollectionResultBuilders |> Map.add typedefof<ResultStream<_>>.Name "BuildStreamResult"

    let private getCollectionResultBuilderName availableBuilders (collectionType: Type) = 
        if collectionType.IsArray then 
            "BuildArrayResult"
        else
            availableBuilders
            |> Map.tryFind (collectionType.GetGenericTypeDefinition().Name)
            |> Option.defaultWith (fun () -> failwith (sprintf "Unsupported collection type %s" collectionType.FullName))

    let generateResultBuilder rowBuilder metadata returnType isAsync = 

        let generateCall returnType methodName (resultBuilder: Expression) = 
            Expression.Call([| returnType |], (methodName + if isAsync then "Async" else ""), resultBuilder)

        let buildOneResultSet collectionResultBuilders metadata returnType = 
            let rec cycle reader metadata prefix name retType = rowBuilder cycle reader metadata prefix name retType
            let buildRow = buildRow cycle
            match returnType with
            | Unit ->
                let result = if isAsync then Expression.UnitAsyncConstant else Expression.UnitConstant           
                Expression.Lambda(result, Expression.Parameter(typeof<IDataReader>)) :> Expression
            | SimpleType ->
                let resultBuilder = buildScalar metadata returnType
                generateCall returnType "BuildSingleResult" resultBuilder :> Expression
            | OptionOf t when isSimpleType t ->               
                let resultBuilder = buildScalar metadata t
                generateCall t "BuildOptionalResult" resultBuilder :> Expression
            | CollectionOf t ->
                let resultBuilder = if isSimpleType t 
                                    then buildScalar metadata t
                                    else buildRow metadata t
                generateCall t (getCollectionResultBuilderName collectionResultBuilders returnType) resultBuilder :> Expression
            | OptionOf t ->
                let resultBuilder = buildRow metadata t
                generateCall t "BuildOptionalResult" resultBuilder :> Expression
            | _ ->
                let resultBuilder = buildRow metadata returnType
                generateCall returnType "BuildSingleResult" resultBuilder :> Expression

        let genResultBuilderCall resultBuilder readerExpr firstResultProcessed = 
            if firstResultProcessed 
            then
                Expression.Invoke(resultBuilder, [readerExpr]) :> Expression
            else
                let nextResultExpr = Expression.Call(readerExpr, "NextResult")
                Expression.Block(nextResultExpr, Expression.Invoke(resultBuilder, [readerExpr])) :> Expression

        let genResultBuilderCallAsync resultBuilder reader firstResultProcessed itemType = 
            if firstResultProcessed
            then
                Expression.Invoke(resultBuilder, [reader]) :> Expression
            else
                let nextResultExpr = Expression.Call(reader, "NextResultAsync")
                let adaptedBuilder = Expression.Lambda(Expression.Invoke(resultBuilder, reader), Expression.Parameter(typeof<bool>))
                Expression.Call([| typeof<bool>; itemType |], "TaskBind", nextResultExpr, adaptedBuilder) :> Expression

        let rec buildMultiResultSetAsync isFirst metadata returnType = 
            let reader = Expression.Parameter(typeof<DbDataReader>, "reader")
            let builders = FSharpType.GetTupleElements returnType 
                            |> Seq.zip metadata
                            |> Seq.mapi (fun i (md, rt) -> 
                                match md with
                                | Direct md -> 
                                    let innerRs = buildOneResultSet multiResultCollectionBuilders md rt
                                    Expression.Parameter(rt, "item" + i.ToString()), genResultBuilderCallAsync innerRs reader (i = 0 && isFirst) rt
                                | Nested md ->
                                    let innerRs = buildMultiResultSetAsync (i = 0 && isFirst) md rt
                                    Expression.Parameter(rt, "item" + i.ToString()), genResultBuilderCallAsync innerRs reader true rt)
                            |> List.ofSeq                            
            let parameters = builders |> List.map fst
            let tupleBuilder = Expression.Call([| returnType |], "AsyncReturn", Expression.NewTuple(parameters |> List.map (fun p -> p :> Expression))) :> Expression   
            
            let bindParam (bld: Expression) (param: ParameterExpression, itemExpr: Expression) = 
                Expression.Call([| param.Type; returnType |], "AsyncBind", itemExpr, Expression.Lambda(bld, param)) :> Expression
                                    
            Expression.Lambda(builders |> List.rev |> List.fold bindParam tupleBuilder, reader) :> Expression

        let rec buildMultiResultSet isFirst metadata returnType = 
            let reader = Expression.Parameter(typeof<IDataReader>, "reader")
            let builders = FSharpType.GetTupleElements returnType 
                            |> Seq.zip metadata
                            |> Seq.mapi (fun i (md, rt) -> 
                                match md with
                                | Direct md ->
                                    genResultBuilderCall (buildOneResultSet multiResultCollectionBuilders md rt) reader (i = 0 && isFirst)
                                | Nested md -> 
                                    genResultBuilderCall (buildMultiResultSet (i = 0 && isFirst) md rt) reader true (* TODO: hack *))
                            |> List.ofSeq
            Expression.Lambda(Expression.NewTuple(builders), reader) :> Expression


        match adaptToReturnType metadata returnType with
        | Direct md ->
            buildOneResultSet singleResultCollectionBuilders md returnType
        | Nested md ->
            match returnType with
            | Tuple _ ->
                if isAsync 
                then buildMultiResultSetAsync true md returnType
                else buildMultiResultSet true md returnType
            | _ ->
                failwith "Function return type for multiple result queries must be a tuple."

