using Dasync.Collections;
using hajk.Data;
using hajk.Fragments;
using Serilog;
using SharpGPX;

namespace hajk.GPX
{
    partial class Menus
    {
        private static int doneCount = 0;
        private static int missingTilesCount = 0;
        private static int totalTilesCount = 0;

        public static async Task DeleteRoute(GPXViewHolder vh)
        {
            Log.Information($"Delete route '{vh.Name.Text}'");

            Show_Dialog msg1 = new(Platform.CurrentActivity);
            if (await msg1.ShowDialog($"Delete", $"Delete '{vh.Name.Text}' ?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
            {
                //Get the GPX information
                var route_to_delete = RouteDatabase.GetRouteAsync(vh.Id).Result;
                GpxClass gpx_to_delete = GpxClass.FromXml(route_to_delete.GPX);

                //Reset counters
                doneCount = 0;
                missingTilesCount = 0;
                totalTilesCount = 0;

                //Progress bar
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync(Platform.CurrentActivity.GetString(Resource.String.UpdatingTiles));
                    Progressbar.UpdateProgressBar.Progress = 0;
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {totalTilesCount}";
                });

                //Tiles to update
                for (int zoom = Fragment_Preferences.MinZoom; zoom <= Fragment_Preferences.MaxZoom; zoom++)
                {
                    AwesomeTiles.TileRange? tiles = GPXUtils.GPXUtils.GetTileRange(zoom, gpx_to_delete);
                    (int TotalTiles, int MissingTiles) = DownloadRasterImageMap.CountTiles(tiles, zoom);
                    totalTilesCount += TotalTiles;
                    missingTilesCount += MissingTiles;
                    Progressbar.UpdateProgressBar.Progress = zoom - Fragment_Preferences.MinZoom + 1;
                    Log.Information($"Need to update '{TotalTiles-MissingTiles}' tiles for zoom level '{zoom}', total to update '{totalTilesCount-missingTilesCount}'");
                }

                //Remove reference in tiles
                for (int zoom = Fragment_Preferences.MinZoom; zoom <= Fragment_Preferences.MaxZoom; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, gpx_to_delete);
                    if (totalTilesCount > 0 && tiles != null)
                    {
                        await tiles.ParallelForEachAsync(async tile =>
                        {
                            await MBTilesWriter.PurgeMapTile(vh.Id, tile);

                            //Update progress counter as the tile is processed, even if unsuccessful
                            Progressbar.UpdateProgressBar.Progress = (int)Math.Ceiling((decimal)(Fragment_Preferences.MaxZoom + (++doneCount) * (100 - Fragment_Preferences.MaxZoom) / (totalTilesCount - missingTilesCount)));
                            Progressbar.UpdateProgressBar.MessageBody = $"({doneCount} of {totalTilesCount - missingTilesCount})";
                        });
                    }
                    else
                    {
                        throw new Exception("How can this be?!?");
                    }
                }

                Progressbar.UpdateProgressBar.Dismiss();
                Log.Debug($"Done updating tiles for {vh.Id}");

                //Remove from route DB
                _ = RouteDatabase.DeleteRouteAsync(vh.Id);

                //Remove from GUI
                Adapter.GpxAdapter.mGpxData.RemoveAt(vh.AdapterPosition);
                Fragment_gpx.mAdapter.NotifyDataSetChanged();
            }
        }
    }
}
