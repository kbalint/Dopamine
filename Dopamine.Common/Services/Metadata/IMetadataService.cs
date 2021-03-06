﻿using Dopamine.Core.Database.Entities;
using Dopamine.Core.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dopamine.Common.Services.Metadata
{
    public interface IMetadataService
    {
        bool IsUpdatingDatabaseMetadata { get; }
        bool IsUpdatingFileMetadata { get; }
        Task UpdateTrackRatingAsync(string path, int rating);
        Task UpdateTrackAsync(List<FileMetadata> fileMetadatas, bool updateAlbumArtwork);
        Task UpdateAlbumAsync(Album album, MetadataArtworkValue artwork, bool updateFileArtwork);
        event Action<MetadataChangedEventArgs> MetadataChanged;
        event Action<RatingChangedEventArgs> RatingChanged;
    }
}
