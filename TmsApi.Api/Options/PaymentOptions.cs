using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Design;
namespace TmsApi.Api.Options;
//namespace TmsApi.Configuration;

public class PaymentOptions
{
    [Required]
    public required string GatewayUrl { get; init; }

    [Range(100, 100000)]
    public decimal MaxDepositBirr { get; init; }
}