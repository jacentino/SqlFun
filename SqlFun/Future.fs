namespace Future

module List = 

    let unfold f state = Seq.unfold f state |> List.ofSeq

    let distinct l = Seq.distinct l |> List.ofSeq

    let groupBy getKey l = Seq.groupBy getKey l 
                            |> Seq.map (fun (k, vseq) -> k, vseq |> List.ofSeq)
                            |> List.ofSeq

    let skip n l = Seq.skip n l |> List.ofSeq

module Seq =

    let except items l = l |> Seq.filter (fun item -> not (Seq.exists (fun it -> item = it) items))

    let tryHead l = if Seq.isEmpty l then None else Some (Seq.head l)
