using System.Security.Cryptography;
using AffiliateSuperstore.Application.Basket;
using Microsoft.AspNetCore.DataProtection;

namespace AffiliateSuperstore.Web.Services;

public sealed class AnonymousBasketStore(
    IDataProtectionProvider dataProtectionProvider,
    AnonymousBasketCodec codec,
    TimeProvider timeProvider)
{
    private const string CookieName = "affiliate-superstore-basket";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("AffiliateSuperstore.AnonymousBasket.v1");

    public IReadOnlyList<string> Get(HttpContext context, string shopSlug)
    {
        return codec.Get(Read(context), shopSlug);
    }

    public void Add(HttpContext context, string shopSlug, string productId)
    {
        Write(context, codec.Add(Read(context), shopSlug, productId));
    }

    public void Remove(HttpContext context, string shopSlug, string productId)
    {
        Write(context, codec.Remove(Read(context), shopSlug, productId));
    }

    public void Clear(HttpContext context, string shopSlug)
    {
        Write(context, codec.Clear(Read(context), shopSlug));
    }

    private string? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private void Write(HttpContext context, string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            context.Response.Cookies.Delete(CookieName);
            return;
        }

        var value = _protector.Protect(serialized);
        context.Response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = timeProvider.GetUtcNow().AddDays(90)
        });
    }

}
