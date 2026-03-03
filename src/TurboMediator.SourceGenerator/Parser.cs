using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TurboMediator.SourceGenerator;

/// <summary>
/// Parses the syntax tree to find handlers and messages.
/// </summary>
internal static class Parser
{
    private static readonly string[] HandlerInterfaces =
    [
        "IRequestHandler",
        "ICommandHandler",
        "IQueryHandler",
        "INotificationHandler",
        "IStreamRequestHandler",
        "IStreamCommandHandler",
        "IStreamQueryHandler"
    ];

    private static readonly string[] MessageInterfaces =
    [
        "IRequest",
        "ICommand",
        "IQuery",
        "INotification",
        "IStreamRequest",
        "IStreamCommand",
        "IStreamQuery"
    ];

    public static bool IsHandlerCandidate(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        // Check if class has any base list (implements interfaces)
        if (classDecl.BaseList is null)
            return false;

        // Check if any base type looks like a handler interface
        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = GetSimpleTypeName(baseType.Type);
            if (HandlerInterfaces.Any(h => typeName.StartsWith(h)))
                return true;
        }

        return false;
    }

    public static bool IsMessageCandidate(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax typeDecl)
            return false;

        // Check if type has any base list (implements interfaces)
        if (typeDecl.BaseList is null)
            return false;

        // Check if any base type looks like a message interface
        foreach (var baseType in typeDecl.BaseList.Types)
        {
            var typeName = GetSimpleTypeName(baseType.Type);
            if (MessageInterfaces.Any(m => typeName.StartsWith(m)))
                return true;
        }

        return false;
    }

    public static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, cancellationToken) as INamedTypeSymbol;
        if (classSymbol is null)
            return null;

        // Detect [RecurringJob] attribute on handler class
        string? recurringJobId = null;
        string? recurringJobCron = null;
        foreach (var attr in classSymbol.GetAttributes())
        {
            var attrFullName = attr.AttributeClass?.ToDisplayString();
            if (attrFullName == "TurboMediator.Scheduling.RecurringJobAttribute" &&
                attr.ConstructorArguments.Length == 2)
            {
                recurringJobId = attr.ConstructorArguments[0].Value as string;
                recurringJobCron = attr.ConstructorArguments[1].Value as string;
                break;
            }
        }

        // Find the handler interface
        foreach (var iface in classSymbol.AllInterfaces)
        {
            var ifaceName = iface.Name;

            if (ifaceName == "IRequestHandler" && iface.TypeArguments.Length >= 1)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments.Length > 1 ? iface.TypeArguments[1] : null;

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType?.Name,
                    responseType?.ToDisplayString(),
                    HandlerKind.Request,
                    IsStreaming: false,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            if (ifaceName == "ICommandHandler" && iface.TypeArguments.Length >= 1)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments.Length > 1 ? iface.TypeArguments[1] : null;

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType?.Name,
                    responseType?.ToDisplayString(),
                    HandlerKind.Command,
                    IsStreaming: false,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            if (ifaceName == "IQueryHandler" && iface.TypeArguments.Length == 2)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments[1];

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    HandlerKind.Query,
                    IsStreaming: false,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            if (ifaceName == "INotificationHandler" && iface.TypeArguments.Length == 1)
            {
                var messageType = iface.TypeArguments[0];

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    ResponseTypeName: null,
                    ResponseTypeFullName: null,
                    HandlerKind.Notification,
                    IsStreaming: false,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            // Stream handlers
            if (ifaceName == "IStreamRequestHandler" && iface.TypeArguments.Length == 2)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments[1];

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    HandlerKind.Request,
                    IsStreaming: true,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            if (ifaceName == "IStreamCommandHandler" && iface.TypeArguments.Length == 2)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments[1];

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    HandlerKind.Command,
                    IsStreaming: true,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }

            if (ifaceName == "IStreamQueryHandler" && iface.TypeArguments.Length == 2)
            {
                var messageType = iface.TypeArguments[0];
                var responseType = iface.TypeArguments[1];

                return new HandlerInfo(
                    classSymbol.Name,
                    classSymbol.ToDisplayString(),
                    messageType.Name,
                    messageType.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    HandlerKind.Query,
                    IsStreaming: true,
                    IsAbstract: classSymbol.IsAbstract,
                    IsPublic: classSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public,
                    RecurringJobId: recurringJobId,
                    RecurringJobCronExpression: recurringJobCron);
            }
        }

        return null;
    }

    public static MessageInfo? GetMessageInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken) as INamedTypeSymbol;
        if (typeSymbol is null)
            return null;

        // Detect known attributes at compile time (zero-reflection scanning)
        bool hasTransactional = false, hasAuditable = false, hasRequiresTenant = false, hasAuthorize = false, hasWithOutbox = false;
        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrFullName = attr.AttributeClass?.ToDisplayString();
            switch (attrFullName)
            {
                case "TurboMediator.Persistence.Transaction.TransactionalAttribute":
                    hasTransactional = true;
                    break;
                case "TurboMediator.Persistence.Audit.AuditableAttribute":
                    hasAuditable = true;
                    break;
                case "TurboMediator.Enterprise.Tenant.RequiresTenantAttribute":
                    hasRequiresTenant = true;
                    break;
                case "TurboMediator.Enterprise.Authorization.AuthorizeAttribute":
                    hasAuthorize = true;
                    break;
                case "TurboMediator.Persistence.Outbox.WithOutboxAttribute":
                    hasWithOutbox = true;
                    break;
            }
        }

        // Find the message interface
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceName = iface.Name;

            // Non-streaming messages
            if (ifaceName == "IRequest")
            {
                var responseType = iface.TypeArguments.Length > 0 ? iface.TypeArguments[0] : null;
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType?.Name,
                    responseType?.ToDisplayString(),
                    MessageKind.Request,
                    IsStreaming: false,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            if (ifaceName == "ICommand")
            {
                var responseType = iface.TypeArguments.Length > 0 ? iface.TypeArguments[0] : null;
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType?.Name,
                    responseType?.ToDisplayString(),
                    MessageKind.Command,
                    IsStreaming: false,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            if (ifaceName == "IQuery" && iface.TypeArguments.Length == 1)
            {
                var responseType = iface.TypeArguments[0];
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    MessageKind.Query,
                    IsStreaming: false,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            if (ifaceName == "INotification")
            {
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    ResponseTypeName: null,
                    ResponseTypeFullName: null,
                    MessageKind.Notification,
                    IsStreaming: false,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            // Streaming messages
            if (ifaceName == "IStreamRequest" && iface.TypeArguments.Length == 1)
            {
                var responseType = iface.TypeArguments[0];
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    MessageKind.Request,
                    IsStreaming: true,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            if (ifaceName == "IStreamCommand" && iface.TypeArguments.Length == 1)
            {
                var responseType = iface.TypeArguments[0];
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    MessageKind.Command,
                    IsStreaming: true,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }

            if (ifaceName == "IStreamQuery" && iface.TypeArguments.Length == 1)
            {
                var responseType = iface.TypeArguments[0];
                return new MessageInfo(
                    typeSymbol.Name,
                    typeSymbol.ToDisplayString(),
                    responseType.Name,
                    responseType.ToDisplayString(),
                    MessageKind.Query,
                    IsStreaming: true,
                    HasTransactional: hasTransactional,
                    HasAuditable: hasAuditable,
                    HasRequiresTenant: hasRequiresTenant,
                    HasAuthorize: hasAuthorize,
                    HasWithOutbox: hasWithOutbox);
            }
        }

        return null;
    }

    private static string GetSimpleTypeName(TypeSyntax type)
    {
        return type switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => type.ToString()
        };
    }
}
