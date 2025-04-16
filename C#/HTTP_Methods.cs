// **************************** BEGIN: PATCH ****************************
/// <summary>
/// Partially update a Message in db via a PATCH call.
/// E.g. OPCRequest payload to replace the IsStarred value: [{ "op": "replace", "path": "/IsStarred", "value": true }]
/// </summary>
/// <param name="messageId">Message identifier</param>
/// <param name="messagePatch">Json Patch document payload</param>
[HttpPatch("{messageId:int}")]
[ProducesResponseType((int)HttpStatusCode.OK)]
[ProducesResponseType((int)HttpStatusCode.InternalServerError)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
public async Task<IActionResult> UpdateMessage(int messageId, [FromBody]JsonPatchDocument<MessageUpdatableDTO> messagePatch)
{
    try
    {
        if (messagePatch == null)
            return BadRequest("Payload is null");

        var party = await _context.Parties
           .Include(p => p.Messages.Where(m => m.MessageId == messageId))
           .FirstOrDefaultAsync(p => p.Profile.PublicSecureUniqueIdentifier == _publicSecureId);

        if (party == null)
            return NotFound("Account not found");

        if (party.Messages != null && party.Messages.Any())
        {
            var msg = party.Messages.First();

            var messageDTO = new MessageUpdatableDTO();
            _mapper.Map(msg, messageDTO);

            messagePatch.ApplyTo(messageDTO);

            _mapper.Map(messageDTO, msg);

            await _context.SaveChangesAsync();
        }

        return Ok();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error occured during message retrieval");

        #if DEBUG
            return StatusCode(500, ex.ToString());
        #else
            return StatusCode(500);
        #endif
    }
}
// **************************** END: PATCH ****************************
