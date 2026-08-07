/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { EntityChangedEventDto } from "./entity-changed-event-dto";

export interface GetEventsSinceResponse {
    events: EntityChangedEventDto[];
    latestSequence: number;
}
