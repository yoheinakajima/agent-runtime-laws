namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws

module TestData =
    let bounded values =
        values
        |> List.truncate 40
        |> List.map (fun value -> value % 1000)

    let event run index kind =
        { Id = EventId(sprintf "%s:event:%06d" run index)
          RunId = RunId run
          Sequence = int64 index
          Kind = kind
          CausedBy = None
          EmittedBy = None }

    let logOfFacts values =
        bounded values
        |> List.mapi (fun index value ->
            event "property" (index + 1) (FactSet("value", value)))

    let descriptor effectId footprint replaySource =
        { Id = EffectId effectId
          Name = effectId
          Footprint = footprint
          ReplaySource = replaySource
          Lifecycle = Requested
          RequestHash = sprintf "request:%s" effectId
          ResponseHash = None }

    let effectLog footprint replaySource =
        let descriptor = descriptor "fx-1" footprint replaySource

        [ event "effects" 1 (EffectRequested descriptor)
          event "effects" 2 (EffectCommitted(descriptor.Id, "response:fx-1")) ]

    let outcomeConfiguration = function
        | Quiescent configuration
        | BlockedAwaitingEffect configuration
        | Diverged(configuration, _) -> configuration
