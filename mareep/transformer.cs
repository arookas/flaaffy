
namespace arookas {

	class Transformer<T> {

		public Transformer<T> Link { get; protected set; }

		public T Transform(T obj) {
			var link = this;

			while (link != null) {
				obj = link.DoTransform(obj);
				link = link.Link;
			}

			return obj;
		}
		protected virtual T DoTransform(T obj) {
			return obj; // pass it along the chain
		}

		public bool AppendLink(Transformer<T> link) {
			if (link == null) {
				return false;
			}

			var current_link = this;

			while (current_link != null) {
				if (current_link == link) {
					return false;
				}
				
				if (current_link.Link == null) {
					break;
				}

				current_link = current_link.Link;
			}

			current_link.Link = link;

			return true;
		}

	}

}
