using TurboMediator;
using TurboMediator.Tests.Messages;

namespace TurboMediator.Tests.Handlers;

// ============ Request Handlers ============

/// <summary>
/// Handler for PingRequest.
/// </summary>
public sealed class PingHandler : IRequestHandler<PingRequest, PongResponse>
{
    public ValueTask<PongResponse> Handle(PingRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<PongResponse>(new PongResponse("Pong!"));
    }
}

/// <summary>
/// Handler for ComplexRequest.
/// </summary>
public sealed class ComplexHandler : IRequestHandler<ComplexRequest, ComplexResponse>
{
    public ValueTask<ComplexResponse> Handle(ComplexRequest request, CancellationToken cancellationToken)
    {
        return new ValueTask<ComplexResponse>(
            new ComplexResponse(request.Id * 2, request.Name.ToUpper(), true));
    }
}

// ============ Command Handlers ============

/// <summary>
/// Handler for CreateItemCommand.
/// </summary>
public sealed class CreateItemHandler : ICommandHandler<CreateItemCommand, CreateItemResult>
{
    public ValueTask<CreateItemResult> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        var result = new CreateItemResult(
            Guid.NewGuid(),
            command.Name,
            command.Price,
            DateTime.UtcNow);

        return new ValueTask<CreateItemResult>(result);
    }
}

/// <summary>
/// Handler for DeleteItemCommand.
/// </summary>
public sealed class DeleteItemHandler : ICommandHandler<DeleteItemCommand, Unit>
{
    public static bool DeleteCalled { get; private set; }
    public static Guid? LastDeletedId { get; private set; }

    public static void Reset()
    {
        DeleteCalled = false;
        LastDeletedId = null;
    }

    public ValueTask<Unit> Handle(DeleteItemCommand command, CancellationToken cancellationToken)
    {
        DeleteCalled = true;
        LastDeletedId = command.Id;
        return new ValueTask<Unit>(Unit.Value);
    }
}

// ============ Query Handlers ============

/// <summary>
/// Handler for GetItemByIdQuery.
/// </summary>
public sealed class GetItemByIdHandler : IQueryHandler<GetItemByIdQuery, ItemDto?>
{
    // Simulated data store
    private static readonly Dictionary<Guid, ItemDto> _items = new();

    public static void AddItem(ItemDto item)
    {
        _items[item.Id] = item;
    }

    public static void Clear()
    {
        _items.Clear();
    }

    public ValueTask<ItemDto?> Handle(GetItemByIdQuery query, CancellationToken cancellationToken)
    {
        _items.TryGetValue(query.Id, out var item);
        return new ValueTask<ItemDto?>(item);
    }
}

/// <summary>
/// Handler for GetAllItemsQuery.
/// </summary>
public sealed class GetAllItemsHandler : IQueryHandler<GetAllItemsQuery, IReadOnlyList<ItemDto>>
{
    private readonly List<ItemDto> _items = new()
    {
        new ItemDto(Guid.NewGuid(), "Item 1", 9.99m),
        new ItemDto(Guid.NewGuid(), "Item 2", 19.99m),
        new ItemDto(Guid.NewGuid(), "Item 3", 29.99m)
    };

    public ValueTask<IReadOnlyList<ItemDto>> Handle(GetAllItemsQuery query, CancellationToken cancellationToken)
    {
        return new ValueTask<IReadOnlyList<ItemDto>>(_items);
    }
}

// ============ Notification Handlers ============

/// <summary>
/// First handler for ItemCreatedNotification.
/// </summary>
public sealed class ItemCreatedLogHandler : INotificationHandler<ItemCreatedNotification>
{
    public static bool WasCalled { get; private set; }
    public static Guid? LastItemId { get; private set; }
    public static string? LastItemName { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
        LastItemId = null;
        LastItemName = null;
    }

    public ValueTask Handle(ItemCreatedNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        LastItemId = notification.Id;
        LastItemName = notification.Name;
        return default;
    }
}

/// <summary>
/// Second handler for ItemCreatedNotification.
/// </summary>
public sealed class ItemCreatedEmailHandler : INotificationHandler<ItemCreatedNotification>
{
    public static bool WasCalled { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
    }

    public ValueTask Handle(ItemCreatedNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return default;
    }
}

/// <summary>
/// Handler for ItemDeletedNotification.
/// </summary>
public sealed class ItemDeletedHandler : INotificationHandler<ItemDeletedNotification>
{
    public static bool WasCalled { get; private set; }
    public static Guid? LastDeletedId { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
        LastDeletedId = null;
    }

    public ValueTask Handle(ItemDeletedNotification notification, CancellationToken cancellationToken)
    {
        WasCalled = true;
        LastDeletedId = notification.Id;
        return default;
    }
}
