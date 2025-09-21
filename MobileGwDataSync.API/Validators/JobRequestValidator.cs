using FluentValidation;
using MobileGwDataSync.API.Models.Requests;
using Quartz;

namespace MobileGwDataSync.API.Validators
{
    /// <summary>
    /// TODO: Валидация запросов для создания/обновления задач синхронизации
    /// Требует установки пакета FluentValidation.AspNetCore
    /// </summary>
    public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
    {
        public CreateJobRequestValidator()
        {
            // TODO: Реализовать правила валидации

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Job name is required")
                .Length(3, 200).WithMessage("Job name must be between 3 and 200 characters");

            RuleFor(x => x.CronExpression)
                .NotEmpty().WithMessage("Cron expression is required")
                .Must(BeValidCronExpression).WithMessage("Invalid cron expression");

            RuleFor(x => x.OneCEndpoint)
                .NotEmpty().WithMessage("1C endpoint is required")
                .Matches(@"^[a-zA-Z0-9_/\-]+$").WithMessage("Invalid endpoint format");

            RuleFor(x => x.Priority)
                .InclusiveBetween(0, 100).WithMessage("Priority must be between 0 and 100");
        }

        private bool BeValidCronExpression(string cronExpression)
        {
            return !string.IsNullOrEmpty(cronExpression) && CronExpression.IsValidExpression(cronExpression);
        }
    }

    public class UpdateJobRequestValidator : AbstractValidator<UpdateJobRequest>
    {
        public UpdateJobRequestValidator()
        {
            // TODO: Реализовать правила валидации для обновления

            RuleFor(x => x.Name)
                .Length(3, 200).When(x => !string.IsNullOrEmpty(x.Name))
                .WithMessage("Job name must be between 3 and 200 characters");

            RuleFor(x => x.CronExpression)
                .Must(BeValidCronExpression).When(x => !string.IsNullOrEmpty(x.CronExpression))
                .WithMessage("Invalid cron expression");
        }

        private bool BeValidCronExpression(string? cronExpression)
        {
            return string.IsNullOrEmpty(cronExpression) || CronExpression.IsValidExpression(cronExpression);
        }
    }
}