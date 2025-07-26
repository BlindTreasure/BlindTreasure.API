// Modern Swagger UI - Complete UX Refactor
const SWAGGER_CONFIG = {
  // Performance settings
  debounceDelay: 300,
  animationDuration: 250,
  maxRetries: 30,
  retryInterval: 100,
  
  // Storage keys
  darkModeKey: 'swagger_dark_mode',
  searchHistoryKey: 'swagger_search_history',
  expandedSectionsKey: 'swagger_expanded_sections',
  
  // UI settings
  maxSearchHistory: 10,
  autoExpandOnFilter: true,
  smoothAnimations: true,
  
  // Selectors
  selectors: {
    swaggerUI: '.swagger-ui',
    infoContainer: '.information-container .main',
    authWrapper: '.auth-wrapper',
    tagSections: '.opblock-tag-section',
    tagButtons: '.opblock-tag',
    operations: '.opblock',
    searchInput: '#apiSearch',
    checkboxContainer: '#checkboxContainer',
    selectAllCheckbox: '#selectAll',
    noResultsMessage: '#noResultsMessage',
    searchContainer: '.custom-search-container'
  }
};

// State management
const AppState = {
  isDarkMode: false,
  searchHistory: [],
  expandedSections: new Set(),
  currentFilter: '',
  selectedTags: new Set(),
  isLoading: false,
  
  init() {
    this.loadFromStorage();
    this.isDarkMode = localStorage.getItem(SWAGGER_CONFIG.darkModeKey) === 'true';
  },
  
  saveToStorage() {
    localStorage.setItem(SWAGGER_CONFIG.darkModeKey, this.isDarkMode);
    localStorage.setItem(SWAGGER_CONFIG.searchHistoryKey, JSON.stringify(this.searchHistory));
    localStorage.setItem(SWAGGER_CONFIG.expandedSectionsKey, JSON.stringify([...this.expandedSections]));
  },
  
  loadFromStorage() {
    try {
      const history = localStorage.getItem(SWAGGER_CONFIG.searchHistoryKey);
      this.searchHistory = history ? JSON.parse(history) : [];
      
      const expanded = localStorage.getItem(SWAGGER_CONFIG.expandedSectionsKey);
      this.expandedSections = expanded ? new Set(JSON.parse(expanded)) : new Set();
    } catch (error) {
      console.warn('Failed to load state from storage:', error);
    }
  }
};

// Utility functions
const Utils = {
  debounce(func, delay) {
    let timeoutId;
    return function (...args) {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => func.apply(this, args), delay);
    };
  },
  
  throttle(func, limit) {
    let inThrottle;
    return function (...args) {
      if (!inThrottle) {
        func.apply(this, args);
        inThrottle = true;
        setTimeout(() => inThrottle = false, limit);
      }
    };
  },
  
  animate(element, properties, duration = SWAGGER_CONFIG.animationDuration) {
    if (!SWAGGER_CONFIG.smoothAnimations) {
      Object.assign(element.style, properties);
      return Promise.resolve();
    }
    
    return new Promise(resolve => {
      const startTime = performance.now();
      const startStyles = {};
      
      // Get computed styles
      Object.keys(properties).forEach(prop => {
        startStyles[prop] = parseFloat(getComputedStyle(element)[prop]) || 0;
      });
      
      function animate(currentTime) {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);
        
        // Easing function (ease-out-cubic)
        const easeProgress = 1 - Math.pow(1 - progress, 3);
        
        Object.keys(properties).forEach(prop => {
          const start = startStyles[prop];
          const end = parseFloat(properties[prop]);
          const current = start + (end - start) * easeProgress;
          element.style[prop] = `${current}${prop.includes('opacity') ? '' : 'px'}`;
        });
        
        if (progress < 1) {
          requestAnimationFrame(animate);
        } else {
          resolve();
        }
      }
      
      requestAnimationFrame(animate);
    });
  },
  
  createElement(tag, className, attributes = {}) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    Object.entries(attributes).forEach(([key, value]) => {
      element.setAttribute(key, value);
    });
    return element;
  },
  
  addSearchToHistory(query) {
    if (!query.trim()) return;
    
    const history = AppState.searchHistory;
    const index = history.indexOf(query);
    if (index > -1) history.splice(index, 1);
    
    history.unshift(query);
    if (history.length > SWAGGER_CONFIG.maxSearchHistory) {
      history.pop();
    }
    
    AppState.searchHistory = history;
    AppState.saveToStorage();
  }
};

// Modern Search Component
class ModernSearch {
  constructor() {
    this.container = null;
    this.searchInput = null;
    this.suggestionsContainer = null;
    this.isSuggestionsVisible = false;
  }
  
  create() {
    const container = Utils.createElement('div', 'modern-search-container');
    
    // Header with icon and title
    const header = Utils.createElement('div', 'search-header');
    const icon = Utils.createElement('span', 'search-icon', { 'aria-hidden': 'true' });
    icon.innerHTML = `
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="11" cy="11" r="8"/>
        <path d="m21 21-4.35-4.35"/>
      </svg>
    `;
    
    const title = Utils.createElement('h3', 'search-title');
    title.textContent = 'API Explorer';
    
    header.appendChild(icon);
    header.appendChild(title);
    container.appendChild(header);
    
    // Search input with suggestions
    const searchWrapper = Utils.createElement('div', 'search-input-wrapper');
    this.searchInput = Utils.createElement('input', 'modern-search-input', {
      type: 'text',
      placeholder: 'Search APIs by endpoint, description, or tag...',
      'aria-label': 'Search APIs'
    });
    
    this.suggestionsContainer = Utils.createElement('div', 'search-suggestions');
    this.suggestionsContainer.style.display = 'none';
    
    searchWrapper.appendChild(this.searchInput);
    searchWrapper.appendChild(this.suggestionsContainer);
    container.appendChild(searchWrapper);
    
    // Filter section
    const filterSection = this.createFilterSection();
    container.appendChild(filterSection);
    
    this.container = container;
    this.bindEvents();
    return container;
  }
  
  createFilterSection() {
    const section = Utils.createElement('div', 'filter-section');
    
    const header = Utils.createElement('div', 'filter-header');
    const title = Utils.createElement('h4', 'filter-title');
    title.textContent = 'Filter by Category';
    
    const clearButton = Utils.createElement('button', 'clear-filters-btn');
    clearButton.textContent = 'Clear All';
    clearButton.addEventListener('click', () => this.clearAllFilters());
    
    header.appendChild(title);
    header.appendChild(clearButton);
    section.appendChild(header);
    
    // Select all checkbox
    const selectAllContainer = Utils.createElement('div', 'select-all-container');
    const selectAllCheckbox = Utils.createElement('input', 'select-all-checkbox', {
      type: 'checkbox',
      id: 'selectAll',
      checked: 'true'
    });
    
    const selectAllLabel = Utils.createElement('label', 'select-all-label', { for: 'selectAll' });
    selectAllLabel.textContent = 'Select All Categories';
    
    selectAllContainer.appendChild(selectAllCheckbox);
    selectAllContainer.appendChild(selectAllLabel);
    section.appendChild(selectAllContainer);
    
    // Tags container
    const tagsContainer = Utils.createElement('div', 'tags-container', { id: 'checkboxContainer' });
    section.appendChild(tagsContainer);
    
    return section;
  }
  
  bindEvents() {
    // Search input events
    this.searchInput.addEventListener('input', Utils.debounce((e) => {
      const query = e.target.value;
      this.handleSearch(query);
      this.showSuggestions(query);
    }, SWAGGER_CONFIG.debounceDelay));
    
    this.searchInput.addEventListener('focus', () => {
      this.showSuggestions(this.searchInput.value);
    });
    
    this.searchInput.addEventListener('blur', () => {
      setTimeout(() => this.hideSuggestions(), 200);
    });
    
    // Keyboard navigation
    this.searchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        this.hideSuggestions();
        this.searchInput.blur();
      }
    });
  }
  
  handleSearch(query) {
    AppState.currentFilter = query;
    Utils.addSearchToHistory(query);
    FilterManager.applyFilters();
  }
  
  showSuggestions(query) {
    if (!query.trim()) {
      this.hideSuggestions();
      return;
    }
    
    const suggestions = this.getSuggestions(query);
    if (suggestions.length === 0) {
      this.hideSuggestions();
      return;
    }
    
    this.suggestionsContainer.innerHTML = '';
    suggestions.forEach(suggestion => {
      const item = Utils.createElement('div', 'suggestion-item');
      item.textContent = suggestion;
      item.addEventListener('click', () => {
        this.searchInput.value = suggestion;
        this.handleSearch(suggestion);
        this.hideSuggestions();
      });
      this.suggestionsContainer.appendChild(item);
    });
    
    this.suggestionsContainer.style.display = 'block';
    this.isSuggestionsVisible = true;
  }
  
  hideSuggestions() {
    this.suggestionsContainer.style.display = 'none';
    this.isSuggestionsVisible = false;
  }
  
  getSuggestions(query) {
    const history = AppState.searchHistory.filter(item => 
      item.toLowerCase().includes(query.toLowerCase())
    );
    
    // Add current endpoints as suggestions
    const endpoints = Array.from(document.querySelectorAll('.opblock-summary-path'))
      .map(el => el.textContent.trim())
      .filter(path => path.toLowerCase().includes(query.toLowerCase()));
    
    return [...new Set([...history, ...endpoints])].slice(0, 5);
  }
  
  clearAllFilters() {
    this.searchInput.value = '';
    AppState.currentFilter = '';
    AppState.selectedTags.clear();
    
    // Reset checkboxes
    document.querySelectorAll('#checkboxContainer input[type="checkbox"]').forEach(cb => {
      cb.checked = true;
    });
    
    FilterManager.applyFilters();
  }
}

// Modern Filter Manager
class FilterManager {
  static init() {
    this.populateTags();
    this.bindEvents();
  }
  
  static populateTags() {
    const container = document.getElementById('checkboxContainer');
    if (!container) return;
    
    const tags = new Set();
    document.querySelectorAll('.opblock-tag-section [data-tag]').forEach(el => {
      tags.add(el.getAttribute('data-tag'));
    });
    
    container.innerHTML = '';
    tags.forEach(tag => {
      const tagElement = this.createTagElement(tag);
      container.appendChild(tagElement);
    });
  }
  
  static createTagElement(tag) {
    const wrapper = Utils.createElement('div', 'tag-filter-item');
    
    const checkbox = Utils.createElement('input', 'tag-checkbox', {
      type: 'checkbox',
      id: `tag-${tag}`,
      value: tag,
      checked: 'true'
    });
    
    const label = Utils.createElement('label', 'tag-label', { for: `tag-${tag}` });
    label.textContent = tag;
    
    const count = Utils.createElement('span', 'tag-count');
    const endpointCount = document.querySelectorAll(`[data-tag="${tag}"] .opblock`).length;
    count.textContent = `(${endpointCount})`;
    
    wrapper.appendChild(checkbox);
    wrapper.appendChild(label);
    wrapper.appendChild(count);
    
    return wrapper;
  }
  
  static bindEvents() {
    const container = document.getElementById('checkboxContainer');
    const selectAll = document.getElementById('selectAll');
    
    if (container) {
      container.addEventListener('change', Utils.debounce(() => {
        this.updateSelectedTags();
        this.applyFilters();
      }, 100));
    }
    
    if (selectAll) {
      selectAll.addEventListener('change', (e) => {
        const isChecked = e.target.checked;
        document.querySelectorAll('#checkboxContainer input[type="checkbox"]').forEach(cb => {
          cb.checked = isChecked;
        });
        this.updateSelectedTags();
        this.applyFilters();
      });
    }
  }
  
  static updateSelectedTags() {
    AppState.selectedTags.clear();
    document.querySelectorAll('#checkboxContainer input[type="checkbox"]:checked').forEach(cb => {
      AppState.selectedTags.add(cb.value);
    });
  }
  
  static applyFilters() {
    if (AppState.isLoading) return;
    
    AppState.isLoading = true;
    
    requestAnimationFrame(() => {
      const sections = document.querySelectorAll(SWAGGER_CONFIG.selectors.tagSections);
      let visibleCount = 0;
      
      sections.forEach(section => {
        const tag = section.querySelector('[data-tag]')?.getAttribute('data-tag');
        const isTagSelected = AppState.selectedTags.has(tag);
        
        if (!isTagSelected) {
          section.style.display = 'none';
          return;
        }
        
        const operations = section.querySelectorAll(SWAGGER_CONFIG.selectors.operations);
        let hasVisibleOperations = false;
        
        operations.forEach(operation => {
          const isVisible = this.isOperationVisible(operation);
          operation.style.display = isVisible ? '' : 'none';
          if (isVisible) hasVisibleOperations = true;
        });
        
        if (hasVisibleOperations) {
          section.style.display = '';
          visibleCount += operations.length;
          
          // Auto-expand if configured
          if (SWAGGER_CONFIG.autoExpandOnFilter && !section.classList.contains('is-open')) {
            this.expandSection(section);
          }
        } else {
          section.style.display = 'none';
        }
      });
      
      this.updateNoResultsMessage(visibleCount);
      this.updateSelectAllState();
      AppState.isLoading = false;
    });
  }
  
  static isOperationVisible(operation) {
    const path = operation.querySelector('[data-path]')?.getAttribute('data-path') || '';
    const description = operation.querySelector('.opblock-summary-description')?.textContent || '';
    const filter = AppState.currentFilter.toLowerCase();
    
    if (!filter) return true;
    
    return path.toLowerCase().includes(filter) || 
           description.toLowerCase().includes(filter);
  }
  
  static expandSection(section) {
    const button = section.querySelector('.opblock-tag');
    if (button && !section.classList.contains('is-open')) {
      button.click();
    }
  }
  
  static updateNoResultsMessage(visibleCount) {
    const message = document.getElementById('noResultsMessage');
    if (!message) return;
    
    if (visibleCount === 0 && AppState.currentFilter) {
      message.style.display = 'block';
      message.innerHTML = `
        <div class="no-results-content">
          <div class="no-results-icon">🔍</div>
          <h3>No APIs found</h3>
          <p>Try adjusting your search terms or filters</p>
        </div>
      `;
    } else {
      message.style.display = 'none';
    }
  }
  
  static updateSelectAllState() {
    const allCheckboxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]');
    const selectAll = document.getElementById('selectAll');
    
    if (selectAll && allCheckboxes.length > 0) {
      const allChecked = Array.from(allCheckboxes).every(cb => cb.checked);
      selectAll.checked = allChecked;
    }
  }
}

// Modern Dark Mode Toggle
class DarkModeToggle {
  constructor() {
    this.toggle = null;
    this.icon = null;
  }
  
  create() {
    const container = Utils.createElement('div', 'dark-mode-container');
    
    const label = Utils.createElement('label', 'dark-mode-toggle');
    const input = Utils.createElement('input', 'dark-mode-input', {
      type: 'checkbox',
      id: 'darkModeToggle'
    });
    
    const slider = Utils.createElement('span', 'dark-mode-slider');
    this.icon = Utils.createElement('span', 'dark-mode-icon');
    this.icon.textContent = '☀️';
    
    slider.appendChild(this.icon);
    label.appendChild(input);
    label.appendChild(slider);
    
    const text = Utils.createElement('span', 'dark-mode-text');
    text.textContent = 'Dark Mode';
    
    container.appendChild(label);
    container.appendChild(text);
    
    this.toggle = input;
    this.bindEvents();
    this.updateState();
    
    return container;
  }
  
  bindEvents() {
    this.toggle.addEventListener('change', () => {
      this.toggleDarkMode();
    });
  }
  
  toggleDarkMode() {
    AppState.isDarkMode = this.toggle.checked;
    this.updateUI();
    AppState.saveToStorage();
  }
  
  updateState() {
    this.toggle.checked = AppState.isDarkMode;
    this.updateUI();
  }
  
  updateUI() {
    const swaggerUI = document.querySelector(SWAGGER_CONFIG.selectors.swaggerUI);
    if (!swaggerUI) return;
    
    if (AppState.isDarkMode) {
      swaggerUI.classList.add('dark-mode');
      this.icon.textContent = '🌙';
    } else {
      swaggerUI.classList.remove('dark-mode');
      this.icon.textContent = '☀️';
    }
  }
}

// Main Application Class
class ModernSwaggerApp {
  constructor() {
    this.search = null;
    this.darkModeToggle = null;
    this.isInitialized = false;
  }
  
  async init() {
    if (this.isInitialized) return;
    
    AppState.init();
    
    // Wait for Swagger UI to be ready
    await this.waitForSwaggerUI();
    
    // Create and inject components
    this.createComponents();
    this.injectComponents();
    
    // Initialize features
    FilterManager.init();
    this.setupEventListeners();
    
    this.isInitialized = true;
    console.log('Modern Swagger UI initialized successfully');
  }
  
  async waitForSwaggerUI() {
    return new Promise((resolve) => {
      let attempts = 0;
      const maxAttempts = SWAGGER_CONFIG.maxRetries;
      
      const checkReady = () => {
        const swaggerUI = document.querySelector(SWAGGER_CONFIG.selectors.swaggerUI);
        const infoContainer = document.querySelector(SWAGGER_CONFIG.selectors.infoContainer);
        
        if (swaggerUI && infoContainer) {
          resolve();
        } else if (++attempts < maxAttempts) {
          setTimeout(checkReady, SWAGGER_CONFIG.retryInterval);
        } else {
          console.warn('Swagger UI not found, proceeding anyway');
          resolve();
        }
      };
      
      checkReady();
    });
  }
  
  createComponents() {
    this.search = new ModernSearch();
    this.darkModeToggle = new DarkModeToggle();
  }
  
  injectComponents() {
    const infoContainer = document.querySelector(SWAGGER_CONFIG.selectors.infoContainer);
    const authWrapper = document.querySelector(SWAGGER_CONFIG.selectors.authWrapper);
    
    if (infoContainer) {
      const searchContainer = this.search.create();
      infoContainer.appendChild(searchContainer);
    }
    
    if (authWrapper) {
      const darkModeContainer = this.darkModeToggle.create();
      authWrapper.insertBefore(darkModeContainer, authWrapper.firstChild);
    }
  }
  
  setupEventListeners() {
    // Handle system preference changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      if (!localStorage.getItem(SWAGGER_CONFIG.darkModeKey)) {
        AppState.isDarkMode = e.matches;
        this.darkModeToggle.updateState();
      }
    });
    
    // Handle window resize
    window.addEventListener('resize', Utils.throttle(() => {
      // Recalculate any layout-dependent elements
    }, 100));
  }
}

// Initialize the application
document.addEventListener('DOMContentLoaded', () => {
  const app = new ModernSwaggerApp();
  app.init().catch(console.error);
});

// Export for potential external use
window.ModernSwaggerApp = ModernSwaggerApp;