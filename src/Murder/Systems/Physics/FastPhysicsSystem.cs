﻿using Bang.Entities;
using Bang.Contexts;
using Bang.Systems;
using Murder.Components;
using Murder.Services;
using Murder.Core;
using Murder.Core.Geometry;
using Murder.Utilities;
using Murder;
using Murder.Messages;

namespace Murder.Systems
{
    [Filter(typeof(PositionComponent), typeof(VelocityComponent))]
    [Filter(ContextAccessorFilter.NoneOf, typeof(AdvancedCollisionComponent))]
    internal class FastPhysicsSystem : IFixedUpdateSystem
    {
        public ValueTask FixedUpdate(Context context)
        {
            Map map = context.World.GetUnique<MapComponent>().Map;

            var collisionEntities = PhysicsServices.FilterPositionAndColliderEntities(context.World, true);

            foreach (Entity e in context.Entities)
            {
                bool ignoreCollisions = false;
                var collider = e.TryGetCollider();
                var id = e.EntityId;


                // If the entity has a velocity, we'll move around by checking for collisions first.
                if (e.TryGetVelocity()?.Velocity is Vector2 rawVelocity)
                {
                    Vector2 velocity = rawVelocity * Game.FixedDeltaTime;
                    PositionComponent relativeStartPosition = e.GetPosition();
                    Vector2 startPosition = relativeStartPosition.GetGlobalPosition().ToVector2();
                    Vector2 newVelocity = rawVelocity;
                    Vector2 shouldMove = Vector2.Zero;

                    if (collider == null || PhysicsServices.CollidesAt(map, id, collider.Value, startPosition, collisionEntities))
                    {
                        ignoreCollisions = true;
                    }

                    if (ignoreCollisions || !PhysicsServices.CollidesAt(map, id, collider!.Value, startPosition + velocity, collisionEntities, out int hitId))
                    {
                        shouldMove = velocity;
                    }
                    else 
                    {
                        e.SendMessage(new CollidedWithMessage(hitId));
                        if (ignoreCollisions || !PhysicsServices.CollidesAt(map, id, collider!.Value, startPosition + new Vector2(velocity.X, 0), collisionEntities))
                        {
                            shouldMove.X = velocity.X;
                        }
                        else
                        {
                            newVelocity.X = newVelocity.X*.5f;
                        }
                        if (ignoreCollisions || !PhysicsServices.CollidesAt(map, id, collider!.Value, startPosition + new Vector2(0, velocity.Y), collisionEntities))
                        {
                            shouldMove.Y = velocity.Y;
                        }
                        else
                        {
                            newVelocity.Y = newVelocity.Y*.5f;
                        }
                    }

                    if (shouldMove.Manhattan()>0)
                        e.SetPosition(new PositionComponent(relativeStartPosition + shouldMove));

                    if (newVelocity.Manhattan() > 0.00001f)
                    {
                        e.SetVelocity(newVelocity);
                    }
                    else
                    {
                        e.RemoveVelocity();
                    }
                }
            }

            return default;
        }

    }
}