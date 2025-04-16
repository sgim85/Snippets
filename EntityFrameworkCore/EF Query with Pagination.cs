/// <summary>
/// Retrieve Inbox messages with pagination support.
/// </summary>
/// <param name="pageNumber">Specifies current pageNumber. Should start with 1.</param>
/// <param name="pageSize">Specifies the maximum pageSize. Default is 10 with a max limit of 100.</param>
/// <param name="search">Search string for filtering. Takes precedence over lastMessageID if both are provided.</param>
/// <param name="isStarred">Boolean to return starred messages or not. Set as null to return both starrred and unstarred.</param>
/// <returns>Inbox messages that match the paging and search request parameters.</returns>
[HttpGet]
[ProducesResponseType(typeof(MessageResponse), (int)HttpStatusCode.OK)]
[ProducesResponseType((int)HttpStatusCode.InternalServerError)]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
public async Task<IActionResult> RetrieveMessages([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = null, [FromQuery] bool? isStarred = null)
{
     if (pageSize <= 0 || pageSize >= 100)
       return BadRequest("Page size is invalid. Must be > 0 and <= 100");

      var party = await _context.Parties
      .Include(p => p.Individual)
      .Include(p => p.Messages.Where(m => m.ExternalMessageId != -999 &&
                                  (search == null || m.Subject.Contains(search) || m.Body.Contains(search)) &&
                                  (isStarred == null || m.IsStarred == isStarred))
                      .OrderByDescending(m => m.MessageId)
                      .Skip(skipPages)
                      //.AsQueryable()
                      .Take(pageSize))
      .FirstOrDefaultAsync(p => p.Profile.PublicSecureUniqueIdentifier == _publicSecureId);

  // More code follows...
}
