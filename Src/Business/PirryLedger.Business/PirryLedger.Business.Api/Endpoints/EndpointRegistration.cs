using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Business.Api;

// RF-NEG-02 + RF-NEG-06: las rutas del negocio pertenecen al modulo del negocio
// (RD-01). Aqui solo se traduce HTTP a una llamada de caso de uso: ninguna regla
// vive en esta capa (RD-02). El acceso lo declara WithAccess y lo resuelve la
// tabla de RoleRequirements; el endpoint no compara roles.
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapBusiness(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/admin/products", (
            ProductBody body,
            CreateProduct useCase,
            IProductImageRepository images,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var product = await useCase.ExecuteAsync(
                    body.Name ?? string.Empty,
                    body.Description,
                    body.Price,
                    cancellationToken);

                // Un producto recien creado todavia no tiene imagen, pero la
                // respuesta lleva el mismo formato que el resto.
                var image = await images.FindLiveAsync(product.Id, cancellationToken);

                return Results.Json(
                    ProductResponse.From(product, image),
                    statusCode: StatusCodes.Status201Created);
            }))
        .WithAccess(Operation.CreateProduct);

        endpoints.MapPut("/api/admin/products/{id:guid}", (
            Guid id,
            ProductBody body,
            UpdateProduct useCase,
            IProductImageRepository images,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var product = await useCase.ExecuteAsync(
                    id,
                    body.Name ?? string.Empty,
                    body.Description,
                    body.Price,
                    cancellationToken);

                var image = await images.FindLiveAsync(id, cancellationToken);

                return Results.Ok(ProductResponse.From(product, image));
            }))
        .WithAccess(Operation.UpdateProduct);

        endpoints.MapPost("/api/admin/products/{id:guid}/active", (
            Guid id,
            ProductActiveBody body,
            SetProductActive useCase,
            IProductImageRepository images,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var product = await useCase.ExecuteAsync(id, body.IsActive, cancellationToken);
                var image = await images.FindLiveAsync(id, cancellationToken);

                return Results.Ok(ProductResponse.From(product, image));
            }))
        .WithAccess(Operation.SetProductActive);

        endpoints.MapGet("/api/billing/products", (
            ListProducts useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var products = await useCase.ExecuteAsync(cancellationToken);
                return Results.Ok(products.Select(ProductResponse.From).ToList());
            }))
        .WithAccess(Operation.ListProducts);

        // ADR 015: la imagen de un producto. Solo el Administrador la sube,
        // la reemplaza o la quita.
        endpoints.MapPut("/api/admin/products/{id:guid}/image", (
            Guid id,
            HttpContext context,
            ProductImageOptions options,
            SetProductImage useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                RaiseBodyLimit(context, options);

                var content = await ReadImageAsync(context.Request, options, cancellationToken);
                var image = await useCase.ExecuteAsync(id, content, cancellationToken);

                return Results.Ok(ProductImageResponse.From(image));
            }))
        .WithAccess(Operation.SetProductImage);

        endpoints.MapDelete("/api/admin/products/{id:guid}/image", (
            Guid id,
            RemoveProductImage useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                await useCase.ExecuteAsync(id, cancellationToken);

                return Results.NoContent();
            }))
        .WithAccess(Operation.RemoveProductImage);

        // ADR 015: la sincronizacion de las imagenes. El cliente manda el
        // cursor que el servidor le dio y recibe lo que cambio: las altas con
        // su firma y las bajas. Cualquier sesion valida puede pedirla: el
        // mostrador necesita las fotos para cobrar.
        endpoints.MapPost("/api/billing/product-images/sync", (
            ProductImageSyncBody? body,
            SyncProductImages useCase,
            HttpContext context,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var result = await useCase.ExecuteAsync(body?.Since, cancellationToken);

                // Las firmas son credenciales de acceso al bucket: la
                // respuesta no se guarda en ninguna cache intermedia.
                context.Response.Headers.CacheControl = "no-store";

                return Results.Ok(ProductImageSyncResponse.From(result));
            }))
        .WithAccess(Operation.SyncProductImages);

        // RF-NEG-02: emitir una factura. El borrador vive solo en el cliente, asi
        // que se guarda ya emitida. Responde 201 con la factura creada.
        endpoints.MapPost("/api/billing/invoices", (
            IssueInvoiceBody body,
            IssueInvoice useCase,
            Authenticate authenticate,
            IBranchDirectory branches,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var branchId = caller.Role == Role.Administrator
                    ? body.BranchId
                    : caller.BranchId;
                var branch = await branches.FindAsync(branchId, cancellationToken)
                    ?? throw new InvalidOperationException("La sucursal indicada no existe.");

                if (branch.IsGlobal)
                {
                    throw new InvalidOperationException("La factura debe pertenecer a una sucursal física.");
                }

                var invoice = await useCase.ExecuteAsync(
                    ToProductItems(body.Items),
                    body.IsDelivery,
                    branch.Id,
                    branch.Name,
                    branch.Address,
                    cancellationToken);

                return Results.Json(
                    InvoiceResponse.From(invoice),
                    statusCode: StatusCodes.Status201Created);
            }))
        .WithAccess(Operation.IssueInvoice);

        // RF-NEG-02: cobrar la factura. El cajero que cobra sale de la sesion,
        // nunca del cuerpo de la peticion.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/collect", (
            Guid id,
            CollectPaymentBody body,
            CollectInvoicePayment useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    ToPayments(body.Payments),
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.CollectInvoicePayment);

        // RF-NEG-02: dejar la factura como deuda, con o sin pago parcial.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/debt", (
            Guid id,
            LeaveAsDebtBody body,
            LeaveInvoiceAsDebt useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var payments = body.Payments is null ? null : ToPayments(body.Payments);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.DebtorName ?? string.Empty,
                    payments,
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.LeaveInvoiceAsDebt);

        // RF-NEG-02: editar los items de una factura emitida. Solo desde Issued;
        // el historial y el aviso al Administrator los resuelve el agregado.
        endpoints.MapPut("/api/billing/invoices/{id:guid}/items", (
            Guid id,
            EditItemsBody body,
            EditInvoiceItems useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    ToProductItems(body.Items),
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.EditInvoiceItems);

        // RF-NEG-02: marcar una factura de delivery como no entregada.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/not-delivered", (
            Guid id,
            MarkInvoiceAsNotDelivered useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.MarkInvoiceAsNotDelivered);

        // RF-NEG-06: anular. Desde Issued no pide permiso; desde Paid o InDebt
        // consume la concesion y, si no la hay, pide el permiso y rechaza con 403
        // (solicitud creada) o 409 (ya habia una pendiente).
        endpoints.MapPost("/api/billing/invoices/{id:guid}/cancel", (
            Guid id,
            CancelInvoice useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(id, caller.Id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.CancelInvoice);

        // RF-NEG-02: el Administrator confirma que la transferencia llego.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/transfers/confirm", (
            Guid id,
            ConfirmInvoiceTransfer useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.ConfirmInvoiceTransfer);

        // RF-NEG-02: el Administrator rechaza una transferencia que no llego.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/transfers/reject", (
            Guid id,
            RejectTransferBody body,
            RejectInvoiceTransfer useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.DebtorName ?? string.Empty,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.RejectInvoiceTransfer);

        // RF-NEG-02: el Administrator registra un abono de deuda.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/payments", (
            Guid id,
            DebtPaymentBody body,
            RegisterDebtPayment useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.Amount,
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.RegisterDebtPayment);

        // RF-NEG-02: consultar facturas. Cualquier sesion valida.
        endpoints.MapGet("/api/billing/invoices", (
            ListInvoices useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);
                Guid? branchId = caller.Role == Role.Administrator
                    ? null
                    : caller.BranchId;
                var invoices = await useCase.ExecuteAsync(branchId, cancellationToken);

                return Results.Ok(invoices.Select(InvoiceResponse.From).ToList());
            }))
        .WithAccess(Operation.ListInvoices);

        endpoints.MapGet("/api/billing/invoices/{id:guid}", (
            Guid id,
            GetInvoice useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);
                Guid? branchId = caller.Role == Role.Administrator
                    ? null
                    : caller.BranchId;
                var invoice = await useCase.ExecuteAsync(id, branchId, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.GetInvoice);

        // RF-NEG-02: el historial de edicion es solo del Administrator.
        endpoints.MapGet("/api/billing/invoices/{id:guid}/edits", (
            Guid id,
            ListInvoiceEdits useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var edits = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(edits.Select(InvoiceEditResponse.From).ToList());
            }))
        .WithAccess(Operation.ListInvoiceEdits);

        return endpoints;
    }

    // Traduce las excepciones conocidas del negocio a un rechazo controlado
    // (RD-07, RD-08). Cualquier otra sube a la frontera de error del API, que la
    // registra y responde un mensaje generico sin trazas.
    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvoiceNotFoundException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ProductNotFoundException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ProductImageNotFoundException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ProductImageTooLargeException error)
        {
            // 413: el archivo es una imagen valida pero no cabe.
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        catch (UnsupportedProductImageException error)
        {
            // 415: el archivo no es una imagen admitida.
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        catch (ImageStorageUnavailableException error)
        {
            // 503: el bucket no responde. El mensaje no dice nada del bucket,
            // del endpoint ni de las credenciales (RD-08).
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ProductInactiveException error)
        {
            return BadRequest(error.Message);
        }
        catch (CancellationPermissionRequiredException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: error.AlreadyPending
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status403Forbidden);
        }
        catch (InvoiceRuleViolationException error)
        {
            return BadRequest(error.Message);
        }
        catch (InvalidProductImageCursorException error)
        {
            // El cursor no lo emitio el servidor: no se puede saber que
            // cambios le faltan al cliente.
            return BadRequest(error.Message);
        }
        catch (InvalidInvoiceTransitionException error)
        {
            return BadRequest(error.Message);
        }
        catch (InvoiceConditionNotMetException error)
        {
            return BadRequest(error.Message);
        }
    }

    private static IResult BadRequest(string message) =>
        Results.Json(
            new ErrorResponse(message),
            statusCode: StatusCodes.Status400BadRequest);

    // Los bytes de sobra que allow_headroom deja para las cabeceras del
    // formulario multipart. No son imagen: son el nombre del archivo, los
    // nombres de los campos y los limites.
    private const long MultipartOverheadBytes = 64 * 1024;

    // La ruta de subida admite mas que el limite general de la aplicacion
    // (1 MiB): una foto de 5 MiB no cabe en el general. El limite se levanta
    // solo en esta ruta y solo hasta el maximo configurado mas el margen de
    // las cabeceras del formulario.
    private static void RaiseBodyLimit(HttpContext context, ProductImageOptions options)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (feature is null || feature.IsReadOnly)
        {
            return;
        }

        feature.MaxRequestBodySize = options.MaxBytes + MultipartOverheadBytes;
    }

    // Lee el archivo de la peticion. El cuerpo ya esta acotado por el limite
    // de la ruta, y aqui se corta la lectura al maximo configurado mas un
    // byte: si hay mas, el archivo es demasiado grande y no se carga entero en
    // memoria.
    private static async Task<byte[]> ReadImageAsync(
        HttpRequest request,
        ProductImageOptions options,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new UnsupportedProductImageException("La peticion debe ser un formulario con el archivo.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (form.Files.Count != 1)
        {
            throw new UnsupportedProductImageException("La peticion debe llevar exactamente un archivo.");
        }

        var file = form.Files[0];

        if (!string.Equals(file.Name, "file", StringComparison.Ordinal))
        {
            throw new UnsupportedProductImageException("El archivo del formulario se llama 'file'.");
        }

        using var buffer = new MemoryStream();
        await using var stream = file.OpenReadStream();

        var limite = options.MaxBytes + 1;
        var chunk = new byte[81920];
        long total = 0;

        while (total < limite)
        {
            var leidos = await stream.ReadAsync(chunk, cancellationToken);

            if (leidos == 0)
            {
                break;
            }

            total += leidos;
            await buffer.WriteAsync(chunk.AsMemory(0, leidos), cancellationToken);
        }

        if (total > options.MaxBytes)
        {
            throw new ProductImageTooLargeException(options.MaxBytes);
        }

        return buffer.ToArray();
    }

    private static IReadOnlyCollection<ProductItemInput> ToProductItems(List<InvoiceItemBody>? items) =>
        items?
            .Select(item => new ProductItemInput(item.ProductId, item.Quantity))
            .ToList()
        ?? new List<ProductItemInput>();

    private static IReadOnlyCollection<InvoicePaymentInput> ToPayments(List<PaymentBody>? payments) =>
        payments?
            .Select(payment => new InvoicePaymentInput(
                ParseMethod(payment.Method),
                payment.Amount,
                payment.ReceivedAmount))
            .ToList()
        ?? new List<InvoicePaymentInput>();

    // PaymentMethod tiene un valor (None) que no es un medio de pago valido para
    // cobrar. Se acepta solo Cash o Transfer; cualquier otra cosa es un 400 con
    // un mensaje en espanol, no un 500 del binding.
    private static PaymentMethod ParseMethod(string? value)
    {
        if (Enum.TryParse<PaymentMethod>(value, ignoreCase: true, out var method)
            && method is PaymentMethod.Cash or PaymentMethod.Transfer)
        {
            return method;
        }

        throw new InvoiceRuleViolationException(
            "El método de pago no es válido; usa Cash o Transfer.");
    }
}
