using System.Security.Claims;
using Application.Dto_s.Message;
using Application.Interfaces.Services.MainServices;
using Microsoft.AspNetCore.SignalR;

namespace HabitHub.Controllers;

public class ChatHub(IMessageService messageService) : Hub
{
    private Guid GetCurrentUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var userId) ? userId : Guid.Empty;
    }

    public async Task SendMessage(Guid recipientId, string text)
    {
        var senderId = GetCurrentUserId();

        var result = await messageService.AddAsync(new MessageAddDto
        {
            SenderId = senderId,
            RecipientId = recipientId,
            Text = text
        });

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
            return;
        }

        var message = await messageService.GetByIdAsync(senderId, result.Value!.Id);
        if (message.IsSuccess)
        {
            await Clients.User(senderId.ToString()).SendAsync("ReceiveMessage", message.Value);
            await Clients.User(recipientId.ToString()).SendAsync("ReceiveMessage", message.Value);
        }
    }

    public async Task EditMessage(Guid messageId, string newText)
    {
        var userId = GetCurrentUserId();

        var result = await messageService.UpdateAsync(userId, new MessagePutDto
        {
            Id = messageId,
            Text = newText
        });

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
            return;
        }

        var updatedMessage = await messageService.GetByIdAsync(userId, messageId);
        if (updatedMessage.IsSuccess)
        {
            var recipientId = updatedMessage.Value!.SenderId == userId
                ? updatedMessage.Value.RecipientId
                : updatedMessage.Value.SenderId;

            await Clients.User(userId.ToString()).SendAsync("UpdateMessage", updatedMessage.Value);
            await Clients.User(recipientId.ToString()).SendAsync("UpdateMessage", updatedMessage.Value);
        }
    }

    public async Task DeleteMessage(Guid messageId)
    {
        var userId = GetCurrentUserId();

        var result = await messageService.DeleteAsync(userId, messageId);

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
            return;
        }

        await Clients.User(userId.ToString()).SendAsync("DeleteMessage", messageId);

        var message = await messageService.GetByIdAsync(userId, messageId);
        if (message.IsSuccess)
        {
            var companionId = message.Value!.SenderId == userId
                ? message.Value.RecipientId
                : message.Value.SenderId;

            await Clients.User(companionId.ToString()).SendAsync("DeleteMessage", messageId);
        }
    }

    public async Task GetChatHistory(Guid companionId)
    {
        var userId = GetCurrentUserId();

        var result = await messageService.GetAllByUsersIdAsync(userId, companionId);

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
            return;
        }

        await Clients.Caller.SendAsync("ChatHistory", companionId, result.Value);
    }

    public async Task GetAllCompanions()
    {
        var userId = GetCurrentUserId();

        var result = await messageService.GetAllCompanionsIdByUserIdAsync(userId);

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
            return;
        }

        var ids = result.Value!.Select(x => x.Id);
        await Clients.Caller.SendAsync("CompanionsList", ids);
    }
}