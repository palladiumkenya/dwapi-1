﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Dwapi.ExtractsManagement.Core.Application.Events;
using Dwapi.ExtractsManagement.Core.Interfaces.Loaders.Dwh;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Source.Dwh;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.ExtractsManagement.Core.Profiles;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Events;
using Dwapi.SharedKernel.Model;
using Dwapi.SharedKernel.Utility;
using MediatR;
using Serilog;

namespace Dwapi.ExtractsManagement.Core.Loader.Dwh
{
    public class EnhancedAdherenceCounsellingLoader : IEnhancedAdherenceCounsellingLoader
    {
        private readonly IEnhancedAdherenceCounsellingExtractRepository _EnhancedAdherenceCounsellingExtractRepository;

        private readonly ITempEnhancedAdherenceCounsellingExtractRepository
            _tempEnhancedAdherenceCounsellingExtractRepository;

        private readonly IMediator _mediator;

        public EnhancedAdherenceCounsellingLoader(
            IEnhancedAdherenceCounsellingExtractRepository EnhancedAdherenceCounsellingExtractRepository,
            ITempEnhancedAdherenceCounsellingExtractRepository tempEnhancedAdherenceCounsellingExtractRepository,
            IMediator mediator)
        {
            _EnhancedAdherenceCounsellingExtractRepository = EnhancedAdherenceCounsellingExtractRepository;
            _tempEnhancedAdherenceCounsellingExtractRepository = tempEnhancedAdherenceCounsellingExtractRepository;
            _mediator = mediator;
        }

        public async Task<int> Load(Guid extractId, int found, bool diffSupport)
        {
            int count = 0; var mapper = diffSupport ? ExtractDiffMapper.Instance : ExtractMapper.Instance;

            try
            {
                DomainEvents.Dispatch(
                    new ExtractActivityNotification(extractId, new DwhProgress(
                        nameof(EnhancedAdherenceCounsellingExtract),
                        nameof(ExtractStatus.Loading),
                        found, 0, 0, 0, 0)));


                StringBuilder query = new StringBuilder();
                query.Append($" SELECT s.* FROM {nameof(TempEnhancedAdherenceCounsellingExtract)}s s");
                query.Append($" INNER JOIN PatientExtracts p ON ");
                query.Append($" s.PatientPK = p.PatientPK AND ");
                query.Append($" s.SiteCode = p.SiteCode ");

                const int take = 1000;
                var eCount = await _tempEnhancedAdherenceCounsellingExtractRepository.GetCount(query.ToString());
                var pageCount = _tempEnhancedAdherenceCounsellingExtractRepository.PageCount(take, eCount);

                int page = 1;
                while (page <= pageCount)
                {
                    var tempEnhancedAdherenceCounsellingExtracts = await
                        _tempEnhancedAdherenceCounsellingExtractRepository.ReadAll(query.ToString(), page, take);

                    var batch = tempEnhancedAdherenceCounsellingExtracts.ToList();
                    count += batch.Count;

                    //Auto mapper
                    var extractRecords = mapper.Map<List<TempEnhancedAdherenceCounsellingExtract>,List<EnhancedAdherenceCounsellingExtract>>(batch);
                    foreach (var record in extractRecords)
                    {
                        record.Id = LiveGuid.NewGuid();
                    }

                    //Batch Insert
                    var inserted = _EnhancedAdherenceCounsellingExtractRepository.BatchInsert(extractRecords);
                    if (!inserted)
                    {
                        Log.Error($"Extract {nameof(EnhancedAdherenceCounsellingExtract)} not Loaded");
                        return 0;
                    }

                    Log.Debug("saved batch");
                    page++;
                    DomainEvents.Dispatch(
                        new ExtractActivityNotification(extractId, new DwhProgress(
                            nameof(EnhancedAdherenceCounsellingExtract),
                            nameof(ExtractStatus.Loading),
                            found, count, 0, 0, 0)));
                }

                await _mediator.Publish(new DocketExtractLoaded("NDWH", nameof(EnhancedAdherenceCounsellingExtract)));

                return count;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Extract {nameof(EnhancedAdherenceCounsellingExtract)} not Loaded");
                return 0;
            }
        }
    }
}

