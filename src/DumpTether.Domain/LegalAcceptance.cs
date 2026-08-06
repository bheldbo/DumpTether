namespace DumpTether.Domain;

public enum LegalDocumentType
{
    TermsOfUse = 1,
    PrivacyNotice = 2
}

public sealed class LegalAcceptance
{
    private LegalAcceptance()
    {
    }

    private LegalAcceptance(
        Guid id,
        Guid userId,
        LegalDocumentType documentType,
        string documentVersion,
        DateTimeOffset acceptedAt)
    {
        Id = id;
        UserId = userId;
        DocumentType = documentType;
        DocumentVersion = documentVersion;
        AcceptedAt = acceptedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public LegalDocumentType DocumentType { get; private set; }

    public string DocumentVersion { get; private set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; private set; }

    public static LegalAcceptance Create(
        Guid userId,
        LegalDocumentType documentType,
        string documentVersion,
        DateTimeOffset acceptedAt)
    {
        if (!Enum.IsDefined(documentType))
        {
            throw new ArgumentOutOfRangeException(nameof(documentType));
        }

        var version = DomainGuards.NotBlank(documentVersion, nameof(documentVersion));
        if (version.Length > 50)
        {
            throw new ArgumentException("Legal document version is too long.", nameof(documentVersion));
        }

        return new LegalAcceptance(
            Guid.NewGuid(),
            userId,
            documentType,
            version,
            acceptedAt);
    }
}
