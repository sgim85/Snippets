// Mapping request payload objects to internal strucutres

// One-way mapping
 public class AccountProfile : AutoMapper.Profile
 {
     public AccountProfile()
     {
         CreateMap<cdp.Profile, AccountDTO>()
             .ReverseMap()
             .ForMember(dest => dest.PublicSecureUniqueIdentifier, opt => opt.Ignore());
     }
 }

// One-way mapping with transformation of value sent in response via a DTO
public class MessageProfile : AutoMapper.Profile
{
   public MessageProfile()
   {
       CreateMap<Message, MessageDTO>()
           .ForMember(dest => dest.MessageDateTime, opt => opt.MapFrom((src, _dst, _, ctx) =>
           {
               TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
               DateTime estTime = src.MessageDateTime;
               try
               {
                   estTime = TimeZoneInfo.ConvertTimeFromUtc(src.MessageDateTime, estZone);
               }
               catch (Exception) { }
               return estTime;
           }))
           .ReverseMap();
   }
}

// Two-way mapping
public class IndividualProfile : AutoMapper.Profile
{
    public IndividualProfile()
    {
        CreateMap<Individual, IndividualDTO>()
            .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender.Name))
            .ForMember(dest => dest.KnownGenderPronoun, opt => opt.MapFrom(src => src.KnownGenderPronoun.Name));

        CreateMap<IndividualDTO, Individual>()
            .ForMember(dest => dest.Gender, opt => opt.Ignore())
            .ForMember(dest => dest.KnownGenderPronoun, opt => opt.Ignore());
    }
}


// Two-way mapping with additional rules/transformations
  public class HealthCardProfile : AutoMapper.Profile
  {
      public HealthCardProfile()
      {
          CreateMap<HealthCard, HealthCardDTO>()
              .ForMember(dest => dest.Number, opt => opt.MapFrom((src, _dst, _, ctx) =>
              {
                  if (ctx.TryGetItems(out Dictionary<string, object> dict))
                      if (dict.ContainsKey("Unmasked"))
                          return src.Number;

                  return Utils.MaskAllCharsExceptLastN(src.Number + src.VersionCode, 3);
              }))
              .ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => Utils.PostalCodeAddSpace(src.PostalCode)))

              .ReverseMap()
              .ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => Utils.PostalCodeRemoveSpaceAndDashes(src.PostalCode)))
              .ForMember(dest => dest.ProductId, opt => opt.Ignore());

          CreateMap<HealthCardDetail, HealthCard>()
             .ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => Utils.PostalCodeAddSpace(src.PostalCode)))
             .ForMember(dest => dest.ModifiedTime, opt => opt.MapFrom(src => src.ModifiedTime))
              .ReverseMap()
              .ForMember(dest => dest.PostalCode, opt => opt.MapFrom(src => Utils.PostalCodeRemoveSpaceAndDashes(src.PostalCode)))
              .ForMember(dest => dest.Product, opt => opt.Ignore()); 

         
      }
  }
