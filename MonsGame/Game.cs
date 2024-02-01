// ∅ 2024 super-metal-mons

namespace MonsGame;
public partial class Game
{
    public Board Board { get; private set; }

    public int WhiteScore { get; private set; }
    public int BlackScore { get; private set; }
    public Color ActiveColor { get; private set; }

    public int ActionsUsedCount { get; private set; }
    public int ManaMovesCount { get; private set; }
    public int MonsMovesCount { get; private set; }

    public int WhitePotionsCount { get; private set; }
    public int BlackPotionsCount { get; private set; }

    public int TurnNumber { get; private set; }

    public Game()
    {
        Board = new Board();
        WhiteScore = 0;
        BlackScore = 0;
        ActiveColor = Color.White;
        ActionsUsedCount = 0;
        ManaMovesCount = 0;
        MonsMovesCount = 0;
        WhitePotionsCount = 0;
        BlackPotionsCount = 0;
        TurnNumber = 1;
    }

    public Game(Board board,
                    int whiteScore,
                    int blackScore,
                    Color activeColor,
                    int actionsUsedCount,
                    int manaMovesCount,
                    int monsMovesCount,
                    int whitePotionsCount,
                    int blackPotionsCount,
                    int turnNumber)
    {
        Board = board;
        WhiteScore = whiteScore;
        BlackScore = blackScore;
        ActiveColor = activeColor;
        ActionsUsedCount = actionsUsedCount;
        ManaMovesCount = manaMovesCount;
        MonsMovesCount = monsMovesCount;
        WhitePotionsCount = whitePotionsCount;
        BlackPotionsCount = blackPotionsCount;
        TurnNumber = turnNumber;
    }

    public Output ProcessInput(List<Input> inputs, bool doNotApplyEvents, bool oneOptionEnough)
    {
        if (WinnerColor(WhiteScore, BlackScore).HasValue) return new InvalidInputOutput();
        if (!inputs.Any()) return SuggestedInputToStartWith();
        if (!(inputs[0] is Input.LocationInput locationInput)) return new InvalidInputOutput();

        var startLocation = locationInput.Location;
        var startItem = Board.GetItem(startLocation);
        if (startItem == null) return new InvalidInputOutput();

        Input? specificSecondInput = inputs.Count > 1 ? inputs[1] : null;
        var secondInputOptions = SecondInputOptions(startLocation, startItem.Value, oneOptionEnough, specificSecondInput);

        if (specificSecondInput == null)
        {
            if (!secondInputOptions.Any())
            {
                return new InvalidInputOutput();
            }
            else
            {
                return new NextInputOptionsOutput(secondInputOptions);
            }
        }

        if (!(specificSecondInput is Input.LocationInput targetLocationInput)) return new InvalidInputOutput();
        var targetLocation = targetLocationInput.Location;
        var secondInputKind = secondInputOptions.Select(opt => (NextInput?)opt).FirstOrDefault(opt => opt.HasValue && opt.Value.Input.Equals(specificSecondInput))?.Kind;
        if (secondInputKind == null) return new InvalidInputOutput();
        Input? specificThirdInput = inputs.Count > 2 ? inputs[2] : null;
        var outputForSecondInput = ProcessSecondInput(secondInputKind.Value, startItem.Value, startLocation, targetLocation, specificThirdInput);
        if (outputForSecondInput == null) return new InvalidInputOutput();
        var thirdInputOptions = outputForSecondInput.Value.Item2;
        var events = outputForSecondInput.Value.Item1;
        if (specificThirdInput == null)
        {
            if (thirdInputOptions.Any())
            {
                return new NextInputOptionsOutput(thirdInputOptions);
            }
            else if (events.Any())
            {
                return new EventsOutput(events);
            }
            else
            {
                return new InvalidInputOutput();
            }
        }

        // TODO: implement missing part

        return new EventsOutput(events);
    }


    // MARK: - process step by step

    private Output SuggestedInputToStartWith()
    {
        Func<Location, Location?> locationsFilter = location =>
        {
            var inputs = new List<Input> { new Input.LocationInput(location) };
            var output = ProcessInput(inputs, doNotApplyEvents: true, oneOptionEnough: true);

            if (output is NextInputOptionsOutput optionsOutput && optionsOutput.NextInputs.Any())
            {
                return location;
            }
            else
            {
                return null;
            }
        };

        var suggestedLocations = Board.AllMonsLocations(ActiveColor).Select(locationsFilter).Where(l => l != null).Cast<Location>().ToList();
        if ((!PlayerCanMoveMon(MonsMovesCount) && !PlayerCanUseAction(TurnNumber, PlayerPotionsCount(ActiveColor, WhitePotionsCount, BlackPotionsCount), ActionsUsedCount) || suggestedLocations.Count == 0) && PlayerCanMoveMana(TurnNumber, ManaMovesCount))
        {
            suggestedLocations.AddRange(Board.AllFreeRegularManaLocations(ActiveColor).Select(locationsFilter).Where(l => l != null).Cast<Location>());
        }

        if (suggestedLocations.Count == 0)
        {
            return new InvalidInputOutput();
        }
        else
        {
            return new LocationsToStartFromOutput(suggestedLocations);
        }
    }


    private HashSet<NextInput> SecondInputOptions(Location startLocation, Item startItem, bool onlyOne, Input? specificNext)
    {
        Location? specificLocation = null;
        if (specificNext is Input.LocationInput locationInput)
        {
            specificLocation = locationInput.Location;
        }

        var startSquare = Board.SquareAt(startLocation);
        HashSet<NextInput> secondInputOptions = new HashSet<NextInput>();

        switch (startItem.Type)
        {
            case ItemType.Mon:
                var mon = startItem.Mon;
                if (mon.color == ActiveColor && !mon.isFainted && PlayerCanMoveMon(MonsMovesCount))
                {
                    secondInputOptions.UnionWith(NextInputs(startLocation.NearbyLocations, NextInputKind.MonMove, onlyOne, specificLocation, location =>
                    {
                        var item = Board.GetItem(location);
                        var square = Board.SquareAt(location);

                        return item switch
                        {
                            { Type: ItemType.Mon or ItemType.MonWithMana or ItemType.MonWithConsumable } => false,
                            { Type: ItemType.Mana } => mon.kind != Mon.Kind.Drainer,
                            { Type: ItemType.Consumable } => true,
                            _ => square.Type switch
                            {
                                SquareType.Regular or SquareType.ConsumableBase or SquareType.ManaBase or SquareType.ManaPool => true,
                                SquareType.SupermanaBase => item?.Mana.Type == ManaType.Supermana && mon.kind == Mon.Kind.Drainer,
                                SquareType.MonBase => mon.kind == item?.MonProperty?.kind && mon.color == item?.MonProperty.Value.color,
                                _ => false,
                            }
                        };
                    }));

                    if (onlyOne && secondInputOptions.Any()) return secondInputOptions;
                }

                // Handling for Mystic, Demon, Spirit actions based on `mon.kind`
                // This section would need significant adaptation to match C# syntax and logic flow.

                break;

            case ItemType.Mana:
                var mana = startItem.ManaProperty;
                if (mana?.Color == ActiveColor && PlayerCanMoveMana(TurnNumber, ManaMovesCount))
                {
                    secondInputOptions.UnionWith(NextInputs(startLocation.NearbyLocations, NextInputKind.ManaMove, onlyOne, specificLocation, location =>
                    {
                        var item = Board.GetItem(location);
                        var square = Board.SquareAt(location);
                        return item switch
                        {
                            { Type: ItemType.Mon } => item?.MonProperty?.kind == Mon.Kind.Drainer,
                            _ => square.Type switch
                            {
                                SquareType.Regular or SquareType.ConsumableBase or SquareType.ManaBase or SquareType.ManaPool => true,
                                _ => false,
                            }
                        };
                    }));
                }

                break;

                // Additional cases for MonWithMana, MonWithConsumable, etc., following similar logic to the above cases.
        }

        return secondInputOptions;
    }


    private (HashSet<Event>, HashSet<NextInput>)? ProcessSecondInput(NextInputKind kind, Item startItem, Location startLocation, Location targetLocation, Input? specificNext)
    {
        Location? specificLocation = null;
        if (specificNext is Input.LocationInput locationInput)
        {
            specificLocation = locationInput.Location;
        }

        HashSet<NextInput> thirdInputOptions = new HashSet<NextInput>();
        HashSet<Event> events = new HashSet<Event>();
        Square targetSquare = Board.SquareAt(targetLocation);
        var targetItem = Board.GetItem(targetLocation); // Adjusted to fit the updated method signature

        switch (kind)
        {
            case NextInputKind.MonMove:
                if (!startItem.MonProperty.HasValue) return null;
                events.Add(new MonMoveEvent(startItem, startLocation, targetLocation));

                if (targetItem.HasValue)
                {
                    var targetItemType = targetItem.Value.Type;
                    switch (targetItemType)
                    {
                        case ItemType.Mon:
                        case ItemType.MonWithMana:
                        case ItemType.MonWithConsumable:
                            return null;

                        case ItemType.Mana:
                            var mana = targetItem.Value.ManaProperty;
                            if (startItem.ManaProperty.HasValue)
                            {
                                var startMana = startItem.ManaProperty.Value;
                                if (startMana.Type == ManaType.Supermana)
                                {
                                    // events.Add(new Event { Type = Event.EventType.SupermanaBackToBase }); // This needs specific event creation
                                }
                                else
                                {
                                    // events.Add(new Event { Type = Event.EventType.ManaDropped }); // This needs specific event creation
                                }
                            }
                            // events.Add(new Event { Type = Event.EventType.PickupMana }); // This needs specific event creation
                            break;

                        case ItemType.Consumable:
                            var consumable = targetItem.Value.ConsumableProperty;
                            switch (consumable)
                            {
                                case Consumable.Potion:
                                case Consumable.Bomb:
                                    return null;

                                case Consumable.BombOrPotion:
                                    if (startItem.ConsumableProperty.HasValue || startItem.ManaProperty.HasValue)
                                    {
                                        // events.Add(new Event { Type = Event.EventType.PickupPotion }); // This needs specific event creation
                                    }
                                    else
                                    {
                                        thirdInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectBomb), NextInputKind.SelectConsumable, startItem));
                                        thirdInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectPotion), NextInputKind.SelectConsumable, startItem));
                                    }
                                    break;
                            }
                            break;
                    }
                }

                if (targetSquare.Type == SquareType.ManaPool && startItem.ManaProperty.HasValue)
                {
                    // events.Add(new Event { Type = Event.EventType.ManaScored }); // This needs specific event creation
                }
                break;

            case NextInputKind.ManaMove:
                // Implementation for ManaMove, assuming it follows a similar pattern to MonMove.
                break;
        }

        return (events, thirdInputOptions);
    }

    private (List<Event>, List<NextInput>)? ProcessThirdInput(NextInput thirdInput, Item startItem, Location startLocation, Location targetLocation)
    {
        var targetItem = Board.GetItem(targetLocation);
        var forthInputOptions = new List<NextInput>();
        var events = new List<Event>();

        switch (thirdInput.Kind)
        {
            case NextInputKind.MonMove:
            case NextInputKind.ManaMove:
            case NextInputKind.MysticAction:
            case NextInputKind.DemonAction:
            case NextInputKind.SpiritTargetCapture:
            case NextInputKind.BombAttack:
                return null;

            case NextInputKind.SpiritTargetMove:
                if (!(thirdInput.Input is Input.LocationInput locationInput) || targetItem == null) return null;

                var destinationLocation = locationInput.Location;
                var destinationItem = Board.GetItem(destinationLocation);
                var destinationSquare = Board.SquareAt(destinationLocation);

                events.Add(new SpiritTargetMoveEvent(targetItem.Value, targetLocation, destinationLocation));

                if (destinationItem.HasValue)
                {
                    switch (destinationItem.Value.Type)
                    {
                        case ItemType.Mon:
                        case ItemType.MonWithMana:
                        case ItemType.MonWithConsumable:
                            return null;

                        case ItemType.Mana:
                            events.Add(new PickupManaEvent(destinationItem.Value.Mana, targetItem.Value.Mon, destinationLocation));
                            break;

                        case ItemType.Consumable:
                            var destinationConsumable = destinationItem.Value.ConsumableProperty;
                            if (destinationConsumable == Consumable.BombOrPotion)
                            {
                                forthInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectBomb), NextInputKind.SelectConsumable, targetItem.Value));
                                forthInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectPotion), NextInputKind.SelectConsumable, targetItem.Value));
                            }
                            break;
                    }
                }

                if (destinationSquare.Type == SquareType.ManaPool && targetItem.Value.ManaProperty.HasValue)
                {
                    events.Add(new ManaScoredEvent(targetItem.Value.ManaProperty.Value, destinationLocation));
                }
                break;

            case NextInputKind.DemonAdditionalStep:
                if (!(thirdInput.Input is Input.LocationInput demonLocationInput) || !startItem.MonProperty.HasValue) return null;
                var destinationLocationDemonStep = demonLocationInput.Location;
                events.Add(new DemonAdditionalStepEvent(startItem.MonProperty.Value, targetLocation, destinationLocationDemonStep));

                var itemAtDestinationDemonStep = Board.GetItem(destinationLocationDemonStep);
                if (itemAtDestinationDemonStep.HasValue && itemAtDestinationDemonStep.Value.Type == ItemType.Consumable && itemAtDestinationDemonStep.Value.ConsumableProperty == Consumable.BombOrPotion)
                {
                    forthInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectBomb), NextInputKind.SelectConsumable, startItem));
                    forthInputOptions.Add(new NextInput(new Input.ModifierInput(Modifier.SelectPotion), NextInputKind.SelectConsumable, startItem));
                }
                break;

            case NextInputKind.SelectConsumable:
                if (!(thirdInput.Input is Input.ModifierInput modifierInput) || !startItem.MonProperty.HasValue) return null;
                switch (modifierInput.Modifier)
                {
                    case Modifier.SelectBomb:
                        events.Add(new PickupBombEvent(startItem.MonProperty.Value, targetLocation));
                        break;
                    case Modifier.SelectPotion:
                        events.Add(new PickupPotionEvent(startItem, targetLocation));
                        break;
                    case Modifier.Cancel:
                        return null;
                }
                break;
        }

        return (events, forthInputOptions);
    }

    private List<Event> ApplyAndAddResultingEvents(List<Event> events)
    {
        void DidUseAction()
        {
            if (ActionsUsedCount >= Config.ActionsPerTurn)
            {
                switch (ActiveColor)
                {
                    case Color.White:
                        WhitePotionsCount--;
                        break;
                    case Color.Black:
                        BlackPotionsCount--;
                        break;
                }
            }
            else
            {
                ActionsUsedCount++;
            }
        }

        List<Event> extraEvents = new List<Event>();

        foreach (Event @event in events)
        {
            switch (@event.Type)
            {
                case Event.EventType.MonMove:
                    MonMoveEvent monMoveEvent = (MonMoveEvent)@event;
                    MonsMovesCount++;
                    Board.RemoveItem(monMoveEvent.From);
                    Board.Put(Item.MonItem(monMoveEvent.Item.Mon), monMoveEvent.To);
                    break;

                case Event.EventType.ManaMove:
                    ManaMoveEvent manaMoveEvent = (ManaMoveEvent)@event;
                    ManaMovesCount++;
                    Board.RemoveItem(manaMoveEvent.From);
                    Board.Put(Item.ManaItem(manaMoveEvent.Mana), manaMoveEvent.To);
                    break;

                case Event.EventType.ManaScored:
                    ManaScoredEvent manaScoredEvent = (ManaScoredEvent)@event;
                    if (ActiveColor == Color.Black)
                    {
                        BlackScore += manaScoredEvent.Mana.Score(Color.Black);
                    }
                    else if (ActiveColor == Color.White)
                    {
                        WhiteScore += manaScoredEvent.Mana.Score(Color.White);
                    }

                    if (Board.GetItem(manaScoredEvent.At)?.MonProperty is Mon mon)
                    {
                        Board.Put(Item.MonItem(mon), manaScoredEvent.At);
                    }
                    else
                    {
                        Board.RemoveItem(manaScoredEvent.At);
                    }
                    break;

                case Event.EventType.MysticAction:
                    MysticActionEvent mysticActionEvent = (MysticActionEvent)@event;
                    DidUseAction();
                    Board.RemoveItem(mysticActionEvent.To);
                    break;

                case Event.EventType.DemonAction:
                    DemonActionEvent demonActionEvent = (DemonActionEvent)@event;
                    DidUseAction();
                    Board.RemoveItem(demonActionEvent.From);
                    Board.Put(Item.MonItem(demonActionEvent.Demon), demonActionEvent.To);
                    break;

                case Event.EventType.DemonAdditionalStep:
                    DemonAdditionalStepEvent demonAdditionalStepEvent = (DemonAdditionalStepEvent)@event;
                    Board.Put(Item.MonItem(demonAdditionalStepEvent.Demon), demonAdditionalStepEvent.To);
                    break;

                case Event.EventType.SpiritTargetMove:
                    SpiritTargetMoveEvent spiritTargetMoveEvent = (SpiritTargetMoveEvent)@event;
                    DidUseAction();
                    Board.RemoveItem(spiritTargetMoveEvent.From);
                    Board.Put(spiritTargetMoveEvent.Item, spiritTargetMoveEvent.To);
                    break;

                case Event.EventType.PickupBomb:
                    PickupBombEvent pickupBombEvent = (PickupBombEvent)@event;
                    Board.Put(Item.MonWithConsumableItem(pickupBombEvent.By, Consumable.Bomb), pickupBombEvent.At);
                    break;

                case Event.EventType.PickupPotion:
                    PickupPotionEvent pickupPotionEvent = (PickupPotionEvent)@event;
                    if (pickupPotionEvent.By.MonProperty?.color == Color.Black)
                    {
                        BlackPotionsCount++;
                    }
                    else if (pickupPotionEvent.By.MonProperty?.color == Color.White)
                    {
                        WhitePotionsCount++;
                    }
                    Board.Put(pickupPotionEvent.By, pickupPotionEvent.At);
                    break;

                case Event.EventType.PickupMana:
                    PickupManaEvent pickupManaEvent = (PickupManaEvent)@event;
                    Board.Put(Item.MonWithManaItem(pickupManaEvent.By, pickupManaEvent.Mana), pickupManaEvent.At);
                    break;

                case Event.EventType.MonFainted:
                    MonFaintedEvent monFaintedEvent = (MonFaintedEvent)@event;
                    monFaintedEvent.Mon.Faint();
                    Board.Put(Item.MonItem(monFaintedEvent.Mon), monFaintedEvent.To);
                    break;

                case Event.EventType.ManaDropped:
                    ManaDroppedEvent manaDroppedEvent = (ManaDroppedEvent)@event;
                    Board.Put(Item.ManaItem(manaDroppedEvent.Mana), manaDroppedEvent.At);
                    break;

                case Event.EventType.SupermanaBackToBase:
                    SupermanaBackToBaseEvent supermanaBackToBaseEvent = (SupermanaBackToBaseEvent)@event;
                    Board.Put(Item.ManaItem(Mana.Supermana), supermanaBackToBaseEvent.To);
                    break;

                case Event.EventType.BombAttack:
                    BombAttackEvent bombAttackEvent = (BombAttackEvent)@event;
                    Board.RemoveItem(bombAttackEvent.To);
                    Board.Put(Item.MonItem(bombAttackEvent.By), bombAttackEvent.From);
                    break;

                case Event.EventType.BombExplosion:
                    BombExplosionEvent bombExplosionEvent = (BombExplosionEvent)@event;
                    Board.RemoveItem(bombExplosionEvent.At);
                    break;

                case Event.EventType.MonAwake:
                case Event.EventType.GameOver:
                case Event.EventType.NextTurn:
                    break;
            }
        }

        if (WinnerColor(WhiteScore, BlackScore) != null)
        {
            extraEvents.Add(new GameOverEvent(WinnerColor(WhiteScore, BlackScore)!.Value));
        }
        else if ((IsFirstTurn(TurnNumber) && !PlayerCanMoveMon(MonsMovesCount)) ||
                 (!IsFirstTurn(TurnNumber) && !PlayerCanMoveMana(TurnNumber, ManaMovesCount)) ||
                 (!IsFirstTurn(TurnNumber) && !PlayerCanMoveMon(MonsMovesCount) && Board.FindMana(ActiveColor) == null))
        {
            ActiveColor = (ActiveColor == Color.White) ? Color.Black : Color.White;
            TurnNumber++;
            extraEvents.Add(new NextTurnEvent(ActiveColor));
            ActionsUsedCount = 0;
            ManaMovesCount = 0;
            MonsMovesCount = 0;

            foreach (Location monLocation in Board.FaintedMonsLocations(ActiveColor))
            {
                if (Board.GetItem(monLocation)?.MonProperty is Mon mon)
                {
                    mon.DecreaseCooldown();
                    Board.Put(Item.MonItem(mon), monLocation);
                    if (!mon.isFainted)
                    {
                        extraEvents.Add(new MonAwakeEvent(mon, monLocation));
                    }
                }
            }
        }

        return events.Concat(extraEvents).ToList();
    }


    // MARK: - helpers

    public List<NextInput> NextInputs(IEnumerable<Location> locations, NextInputKind kind, bool onlyOne, Location? specific, Func<Location, bool> filter)
    {
        var inputs = new List<NextInput>();
        if (specific != null)
        {
            if (locations.Contains(specific.Value) && filter(specific.Value))
            {
                var input = new Input.LocationInput(specific.Value);
                inputs.Add(new NextInput(input, kind));
            }
        }
        else if (onlyOne)
        {
            var one = locations.FirstOrDefault(loc => filter(loc));
            if (!one.Equals(default(Location)))
            {
                var input = new Input.LocationInput(one);
                inputs.Add(new NextInput(input, kind));
            }
        }
        else
        {
            inputs.AddRange(locations.Where(loc => filter(loc)).Select(loc =>
            {
                var input = new Input.LocationInput(loc);
                return new NextInput(input, kind);
            }));
        }
        return inputs;
    }


    public Dictionary<AvailableMoveKind, int> AvailableMoveKinds(int monsMovesCount, int actionsUsedCount, int manaMovesCount, int playerPotionsCount, int turnNumber)
    {
        var moves = new Dictionary<AvailableMoveKind, int>
        {
            [AvailableMoveKind.MonMove] = Config.MonsMovesPerTurn - monsMovesCount,
            [AvailableMoveKind.Action] = 0,
            [AvailableMoveKind.Potion] = 0,
            [AvailableMoveKind.ManaMove] = 0
        };

        if (turnNumber != 1)
        {
            moves[AvailableMoveKind.Action] = Config.ActionsPerTurn - actionsUsedCount;
            moves[AvailableMoveKind.Potion] = playerPotionsCount;
            moves[AvailableMoveKind.ManaMove] = Config.ManaMovesPerTurn - manaMovesCount;
        }

        return moves;
    }

    public Color? WinnerColor(int whiteScore, int blackScore)
    {
        if (whiteScore >= Config.TargetScore)
        {
            return Color.White;
        }
        else if (blackScore >= Config.TargetScore)
        {
            return Color.Black;
        }
        else
        {
            return null;
        }
    }

    public bool IsFirstTurn(int turnNumber) => turnNumber == 1;

    public int PlayerPotionsCount(Color activeColor, int whitePotionsCount, int blackPotionsCount)
        => activeColor == Color.White ? whitePotionsCount : blackPotionsCount;

    public bool PlayerCanMoveMon(int monsMovesCount) => monsMovesCount < Config.MonsMovesPerTurn;

    public bool PlayerCanMoveMana(int turnNumber, int manaMovesCount)
        => turnNumber != 1 && manaMovesCount < Config.ManaMovesPerTurn;

    public bool PlayerCanUseAction(int turnNumber, int playerPotionsCount, int actionsUsedCount)
        => turnNumber != 1 && (playerPotionsCount > 0 || actionsUsedCount < Config.ActionsPerTurn);

    public HashSet<Location> ProtectedByOpponentsAngel(Board board, Color activeColor)
    {
        var protectedLocations = new HashSet<Location>();
        var angelLocation = board.FindAwakeAngel(activeColor.Other());
        if (angelLocation != null)
        {
            protectedLocations.UnionWith(angelLocation.Value.NearbyLocations);
        }
        return protectedLocations;
    }

}
