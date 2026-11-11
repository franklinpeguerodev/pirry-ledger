namespace PirryLedger.Core.Documents.Application.Exceptions;

// RF-DOC-03 / RF-DOC-04: el documento no existe, esta eliminado o su objeto ya
// no esta en el almacenamiento. Las tres cosas son el mismo 404 para quien
// llama: un documento eliminado no se descarga ni se vuelve a eliminar.
public sealed class DocumentNotFoundException : Exception
{
    public DocumentNotFoundException()
        : base("El documento no existe.")
    {
    }
}
