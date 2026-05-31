

    using System.Collections.Generic;

    public interface IEntityPathMover
    {
        /// <summary>
        /// Initiates movement along a provided path.
        /// </summary>
        /// <param name="path">The list of PathNodes to traverse.</param>
        public void StartMoving(List<PathNode> path);

        /// <summary>
        /// Stops any ongoing movement.
        /// </summary>
        public void StopMoving();

        /// <summary>
        /// Returns true if the mover is currently traversing a path.
        /// </summary>
        public bool IsMoving();

        /// <summary>
        /// Handles the continuous movement and steering of the vehicle.
        /// </summary>
        public void MoveAlongPath();
    }