using AutoMapper;
using MobileGwDataSync.Integration.Models;

namespace MobileGwDataSync.Integration.OneC
{
    public class OneCMappingProfile : Profile
    {
        public OneCMappingProfile()
        {
            CreateMap<OneCSubscriber, Dictionary<string, object>>()
                .ConvertUsing<OneCSubscriberConverter>();
        }
    }

    public class OneCSubscriberConverter : ITypeConverter<OneCSubscriber, Dictionary<string, object>>
    {
        public Dictionary<string, object> Convert(OneCSubscriber source, Dictionary<string, object> destination, ResolutionContext context)
        {
            return new Dictionary<string, object>
            {
                ["Account"] = source.Account,
                ["FIO"] = source.FIO,
                ["Address"] = source.Address,
                ["Balance"] = source.Balance
            };
        }
    }
}
